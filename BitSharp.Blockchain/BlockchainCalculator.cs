using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Data;
using System.IO;
using BitSharp.Storage;
using System.Globalization;
using System.Collections;

namespace BitSharp.Blockchain
{
    public class BlockchainCalculator
    {
        private readonly IBlockchainRules _rules;
        private readonly CacheContext _cacheContext;
        private readonly CancellationToken shutdownToken;

        public BlockchainCalculator(IBlockchainRules rules, CacheContext cacheContext, CancellationToken shutdownToken)
        {
            this._rules = rules;
            this._cacheContext = cacheContext;
            this.shutdownToken = shutdownToken;
        }

        public IBlockchainRules Rules { get { return this._rules; } }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public void CalculateBlockchainFromExisting(ChainStateBuilder chainStateBuilder, Func<ChainedBlocks> getTargetChainedBlocks, CancellationToken cancelToken, Action onProgress = null)
        {
            chainStateBuilder.Stats.totalStopwatch.Start();
            chainStateBuilder.Stats.currentRateStopwatch.Start();

            // calculate the new blockchain along the target path
            bool utxoSafe = true;
            try
            {
                foreach (var pathElement in BlockAndInputsLookAhead(chainStateBuilder.ChainedBlocks.NavigateTowards(getTargetChainedBlocks), maxLookAhead: 100))
                {
                    utxoSafe = false;

                    // cooperative loop
                    if (this.shutdownToken.IsCancellationRequested)
                        break;
                    if (cancelToken.IsCancellationRequested)
                        break;

                    // get block and metadata for next link in blockchain
                    var direction = pathElement.Item1;
                    var chainedBlock = pathElement.Item2;
                    var block = pathElement.Item3;
                    var prevInputTxes = pathElement.Item4;

                    if (direction < 0)
                    {
                        List<TxOutputKey> spendOutputs, receiveOutputs;
                        RollbackUtxo(chainStateBuilder, block, out spendOutputs, out receiveOutputs);

                        chainStateBuilder.ChainedBlocks.RemoveBlock(chainedBlock);
                    }
                    else if (direction > 0)
                    {
                        // calculate the new block utxo, double spends will be checked for
                        ImmutableDictionary<UInt256, ImmutableHashSet<int>> newTransactions = ImmutableDictionary.Create<UInt256, ImmutableHashSet<int>>();
                        long txCount = 0, inputCount = 0;
                        new MethodTimer(false).Time("CalculateUtxo", () =>
                            CalculateUtxo(chainedBlock.Height, block, chainStateBuilder.Utxo, out newTransactions, out txCount, out inputCount));

                        chainStateBuilder.ChainedBlocks.AddBlock(chainedBlock);

                        // validate the block
                        // validation utxo includes all transactions added in the same block, any double spends will have failed the block above
                        chainStateBuilder.Stats.validateStopwatch.Start();
                        try
                        {
                            new MethodTimer(false).Time("ValidateBlock", () =>
                                this.Rules.ValidateBlock(block, chainStateBuilder, newTransactions, prevInputTxes));
                        }
                        finally
                        {
                            chainStateBuilder.Stats.validateStopwatch.Stop();
                        }

                        // flush utxo progress
                        chainStateBuilder.Utxo.Flush();

                        // create the next link in the new blockchain
                        if (onProgress != null)
                            onProgress();

                        // blockchain processing statistics
                        chainStateBuilder.Stats.currentBlockCount++;
                        chainStateBuilder.Stats.currentTxCount += txCount;
                        chainStateBuilder.Stats.currentInputCount += inputCount;
                        chainStateBuilder.Stats.totalTxCount += txCount;
                        chainStateBuilder.Stats.totalInputCount += inputCount;

                        var txInterval = 100.THOUSAND();
                        if (
                            chainStateBuilder.ChainedBlocks.Height % 10.THOUSAND() == 0
                            || (chainStateBuilder.Stats.totalTxCount % txInterval < (chainStateBuilder.Stats.totalTxCount - txCount) % txInterval || txCount >= txInterval))
                        {
                            LogBlockchainProgress(chainStateBuilder);

                            chainStateBuilder.Stats.currentBlockCount = 0;
                            chainStateBuilder.Stats.currentTxCount = 0;
                            chainStateBuilder.Stats.currentInputCount = 0;
                            chainStateBuilder.Stats.currentRateStopwatch.Reset();
                            chainStateBuilder.Stats.currentRateStopwatch.Start();
                        }
                    }
                    else
                        throw new InvalidOperationException();
                    utxoSafe = true;
                }
            }
            catch (Exception)
            {
                if (!utxoSafe)
                    throw;
            }

            if (onProgress != null)
                onProgress();

            LogBlockchainProgress(chainStateBuilder);
            chainStateBuilder.Stats.totalStopwatch.Stop();
            chainStateBuilder.Stats.currentRateStopwatch.Stop();
        }

        private void LogBlockchainProgress(ChainStateBuilder chainStateBuilder)
        {
            var currentBlockRate = (float)chainStateBuilder.Stats.currentBlockCount / chainStateBuilder.Stats.currentRateStopwatch.ElapsedSecondsFloat();
            var currentTxRate = (float)chainStateBuilder.Stats.currentTxCount / chainStateBuilder.Stats.currentRateStopwatch.ElapsedSecondsFloat();
            var currentInputRate = (float)chainStateBuilder.Stats.currentInputCount / chainStateBuilder.Stats.currentRateStopwatch.ElapsedSecondsFloat();

            Debug.WriteLine(
                string.Join("\n",
                    new string('-', 80),
                    "Height: {0,10} | Duration: {1} hh:mm:ss | Validation: {2} hh:mm:ss | Blocks/s: {3,7} | Tx/s: {4,7} | Inputs/s: {5,7} | Total Tx: {6,7} | Total Inputs: {7,7} | Utxo Size: {8,7}",
                    "GC Memory:      {9,10:#,##0.00} MB",
                    "Process Memory: {10,10:#,##0.00} MB",
                    new string('-', 80)
                )
                .Format2
                (
                /*0*/ chainStateBuilder.ChainedBlocks.Height.ToString("#,##0"),
                /*1*/ chainStateBuilder.Stats.totalStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*2*/ chainStateBuilder.Stats.validateStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*3*/ currentBlockRate.ToString("#,##0"),
                /*4*/ currentTxRate.ToString("#,##0"),
                /*5*/ currentInputRate.ToString("#,##0"),
                /*6*/ chainStateBuilder.Stats.totalTxCount.ToString("#,##0"),
                /*7*/ chainStateBuilder.Stats.totalInputCount.ToString("#,##0"),
                /*8*/ chainStateBuilder.Utxo.Count.ToString("#,##0"),
                /*9*/ (float)GC.GetTotalMemory(false) / 1.MILLION(),
                /*10*/ (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()
                ));
        }

        private void CalculateUtxo(long blockHeight, Block block, UtxoBuilder utxoBuilder, out ImmutableDictionary<UInt256, ImmutableHashSet<int>> newTransactions, out long txCount, out long inputCount)
        {
            txCount = 0;
            inputCount = 0;

            var newTransactionsBuilder = ImmutableDictionary.CreateBuilder<UInt256, ImmutableHashSet<int>>();

            // don't include genesis block coinbase in utxo
            if (blockHeight > 0)
            {
                //TODO apply real coinbase rule
                // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                var coinbaseTx = block.Transactions[0];

                // add the coinbase outputs to the utxo
                var coinbaseUnspentTx = new UnspentTx(coinbaseTx.Hash, new ImmutableBitArray(coinbaseTx.Outputs.Count, true));

                // add transaction output to to the utxo
                if (utxoBuilder.ContainsKey(coinbaseTx.Hash))
                {
                    // duplicate transaction output
                    Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(blockHeight, block.Hash.ToHexNumberString()));

                    //TODO the inverse needs to be special cased in RollbackUtxo as well
                    if ((blockHeight == 91842 && coinbaseTx.Hash == UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber))
                        || (blockHeight == 91880 && coinbaseTx.Hash == UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber)))
                    {
                        utxoBuilder.Remove(coinbaseTx.Hash);
                    }
                    else
                    {
                        throw new ValidationException();
                    }
                }

                newTransactionsBuilder.Add(coinbaseTx.Hash, ImmutableHashSet.Create(0));
                utxoBuilder.Add(coinbaseTx.Hash, coinbaseUnspentTx);
            }

            // check for double spends
            for (var txIndex = 1; txIndex < block.Transactions.Count; txIndex++)
            {
                var tx = block.Transactions[txIndex];
                txCount++;

                for (var inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                {
                    var input = tx.Inputs[inputIndex];
                    inputCount++;

                    if (!utxoBuilder.ContainsKey(input.PreviousTxOutputKey.TxHash))
                    {
                        // output wasn't present in utxo, invalid block
                        throw new ValidationException();
                    }

                    var prevUnspentTx = utxoBuilder[input.PreviousTxOutputKey.TxHash];

                    if (input.PreviousTxOutputKey.TxOutputIndex >= prevUnspentTx.UnspentOutputs.Length)
                    {
                        // output was out of bounds
                        throw new ValidationException();
                    }

                    if (!prevUnspentTx.UnspentOutputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()])
                    {                        // output was already spent
                        throw new ValidationException();
                    }


                    // remove the output from the utxo
                    utxoBuilder[input.PreviousTxOutputKey.TxHash] =
                        new UnspentTx(prevUnspentTx.TxHash, prevUnspentTx.UnspentOutputs.Set(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), false));

                    // remove fully spent transaction from the utxo
                    if (utxoBuilder[input.PreviousTxOutputKey.TxHash].UnspentOutputs.All(x => !x))
                        utxoBuilder.Remove(input.PreviousTxOutputKey.TxHash);
                }

                // add the output to the list to be added to the utxo
                var unspentTx = new UnspentTx(tx.Hash, new ImmutableBitArray(tx.Outputs.Count, true));

                // add transaction output to to the utxo
                if (utxoBuilder.ContainsKey(tx.Hash))
                {
                    // duplicate transaction output
                    Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, tx {2}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex));
                    //Debugger.Break();
                    //TODO throw new Validation();

                    //TODO this needs to be tracked so that blocks can be rolled back accurately
                    //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                }

                if (!newTransactionsBuilder.ContainsKey(tx.Hash))
                    newTransactionsBuilder.Add(tx.Hash, ImmutableHashSet.Create(txIndex));
                else
                    newTransactionsBuilder[tx.Hash] = newTransactionsBuilder[tx.Hash].Add(txIndex);
                utxoBuilder.Add(tx.Hash, unspentTx);
            }

            // validation successful, return the new utxo
            newTransactions = newTransactionsBuilder.ToImmutable();
        }

        private void RollbackUtxo(ChainStateBuilder chainStateBuilder, Block block, out List<TxOutputKey> spendOutputs, out List<TxOutputKey> receiveOutputs)
        {
            var blockHeight = chainStateBuilder.ChainedBlocks.Height;
            var utxoBuilder = chainStateBuilder.Utxo;

            spendOutputs = new List<TxOutputKey>();
            receiveOutputs = new List<TxOutputKey>();

            //TODO apply real coinbase rule
            // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
            var coinbaseTx = block.Transactions[0];

            for (var outputIndex = 0; outputIndex < coinbaseTx.Outputs.Count; outputIndex++)
            {
                var txOutputKey = new TxOutputKey(coinbaseTx.Hash, (UInt32)outputIndex);
                if (blockHeight > 0)
                {
                    // remove new outputs from the rolled back utxo
                    if (utxoBuilder.Remove(coinbaseTx.Hash))
                    {
                        receiveOutputs.Add(txOutputKey);
                    }
                    else
                    {
                        // missing transaction output
                        Debug.WriteLine("Missing transaction at block {0:#,##0}, {1}, tx {2}, output {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), 0, outputIndex));
                        Debugger.Break();
                        //TODO throw new Validation();

                        //TODO this needs to be tracked so that blocks can be rolled back accurately
                        //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    }
                }
            }

            for (var txIndex = block.Transactions.Count - 1; txIndex >= 1; txIndex--)
            {
                var tx = block.Transactions[txIndex];

                for (var outputIndex = tx.Outputs.Count - 1; outputIndex >= 0; outputIndex--)
                {
                    var output = tx.Outputs[outputIndex];
                    var txOutputKey = new TxOutputKey(tx.Hash, (UInt32)outputIndex);
                    //TODO what if a transaction wasn't added to the utxo because it already existed?
                    //TODO the block would still pass without adding the tx to its utxo, but here it would get rolled back
                    //TODO maybe a flag bit to track this?

                    // remove new outputs from the rolled back utxo
                    if (utxoBuilder.Remove(tx.Hash))
                    {
                        receiveOutputs.Add(txOutputKey);
                    }
                    else
                    {
                        // missing transaction output
                        Debug.WriteLine("Missing transaction at block {0:#,##0}, {1}, tx {2}, output {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, outputIndex));
                        Debugger.Break();
                        //TODO throw new Validation();

                        //TODO this needs to be tracked so that blocks can be rolled back accurately
                        //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    }
                }

                for (var inputIndex = tx.Inputs.Count - 1; inputIndex >= 0; inputIndex--)
                {
                    var input = tx.Inputs[inputIndex];

                    // add spent outputs back into the rolled back utxo
                    if (utxoBuilder.ContainsKey(input.PreviousTxOutputKey.TxHash))
                    {
                        var prevUnspentTx = utxoBuilder[input.PreviousTxOutputKey.TxHash];

                        // check if output is out of bounds
                        if (input.PreviousTxOutputKey.TxOutputIndex >= prevUnspentTx.UnspentOutputs.Length)
                            throw new ValidationException();

                        // check that output isn't already considered unspent
                        if (prevUnspentTx.UnspentOutputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()])
                            throw new ValidationException();

                        // mark output as unspent
                        utxoBuilder[input.PreviousTxOutputKey.TxHash] =
                            new UnspentTx(prevUnspentTx.TxHash, prevUnspentTx.UnspentOutputs.Set(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), true));
                    }
                    else
                    {
                        // fully spent transaction being added back in during roll back
                        var prevUnspentTx = this.CacheContext.TransactionCache[input.PreviousTxOutputKey.TxHash];

                        utxoBuilder[input.PreviousTxOutputKey.TxHash] =
                            new UnspentTx(prevUnspentTx.Hash, new ImmutableBitArray(prevUnspentTx.Outputs.Count, false).Set(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), true));
                    }

                    //TODO
                    //if (prevUtxoBuilder.Add(input.PreviousTxOutputKey))
                    //{
                    //    spendOutputs.Add(input.PreviousTxOutputKey);
                    //}
                    //else
                    //{
                    //    // missing transaction output
                    //    Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, tx {2}, input {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, inputIndex));
                    //    Debugger.Break();
                    //    //TODO throw new Validation();

                    //    //TODO this needs to be tracked so that blocks can be rolled back accurately
                    //    //TODO track these separately on the blockchain info? gonna be costly to track on every transaction
                    //}
                }
            }
        }

        public void RevalidateBlockchain(ChainedBlocks blockchain, Block genesisBlock)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                //TODO delete corrupted data? could get stuck in a fail-loop on the winning chain otherwise

                // verify blockchain has blocks
                if (blockchain.BlockList.Count == 0)
                    throw new ValidationException();

                // verify genesis block hash
                if (blockchain.BlockList[0].BlockHash != genesisBlock.Hash)
                    throw new ValidationException();

                // get genesis block header
                var chainGenesisBlockHeader = this.CacheContext.BlockHeaderCache[blockchain.BlockList[0].BlockHash];

                // verify genesis block header
                if (
                    genesisBlock.Header.Version != chainGenesisBlockHeader.Version
                    || genesisBlock.Header.PreviousBlock != chainGenesisBlockHeader.PreviousBlock
                    || genesisBlock.Header.MerkleRoot != chainGenesisBlockHeader.MerkleRoot
                    || genesisBlock.Header.Time != chainGenesisBlockHeader.Time
                    || genesisBlock.Header.Bits != chainGenesisBlockHeader.Bits
                    || genesisBlock.Header.Nonce != chainGenesisBlockHeader.Nonce
                    || genesisBlock.Hash != chainGenesisBlockHeader.Hash
                    || genesisBlock.Hash != CalculateHash(chainGenesisBlockHeader))
                {
                    throw new ValidationException();
                }

                // setup expected previous block hash value to verify each chain actually does link
                var expectedPreviousBlockHash = genesisBlock.Header.PreviousBlock;
                for (var height = 0; height < blockchain.BlockList.Count; height++)
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();

                    // get the current link in the chain
                    var chainedBlock = blockchain.BlockList[height];

                    // verify height
                    if (chainedBlock.Height != height)
                        throw new ValidationException();

                    // verify blockchain linking
                    if (chainedBlock.PreviousBlockHash != expectedPreviousBlockHash)
                        throw new ValidationException();

                    // verify block exists
                    var blockHeader = this.CacheContext.BlockHeaderCache[chainedBlock.BlockHash];

                    // verify block metadata matches header values
                    if (blockHeader.PreviousBlock != chainedBlock.PreviousBlockHash)
                        throw new ValidationException();

                    // verify block header hash
                    if (CalculateHash(blockHeader) != chainedBlock.BlockHash)
                        throw new ValidationException();

                    // next block metadata should have the current metadata's hash as its previous hash value
                    expectedPreviousBlockHash = chainedBlock.BlockHash;
                }

                // all validation passed
            }
            finally
            {
                stopwatch.Stop();
                Debug.WriteLine("Blockchain revalidation: {0:#,##0.000000}s".Format2(stopwatch.ElapsedSecondsFloat()));
            }
        }

        public IEnumerable<Tuple<int, ChainedBlock, Block, ImmutableDictionary<UInt256, Transaction>>> BlockAndInputsLookAhead(IEnumerable<Tuple<int, ChainedBlock>> chainedBlocks, int maxLookAhead)
        {
            return chainedBlocks
                .Select(
                    chainedBlockTuple =>
                    {
                        var chainedBlockDirection = chainedBlockTuple.Item1;
                        var chainedBlock = chainedBlockTuple.Item2;

                        var block = new MethodTimer(false).Time("GetBlock", () =>
                            this.CacheContext.BlockCache[chainedBlock.BlockHash]);

                        var prevInputTxes = ImmutableDictionary.CreateBuilder<UInt256, Transaction>();
                        new MethodTimer(false).Time("GetPrevInputTxes", () =>
                        {
                            foreach (var prevInput in block.Transactions.Skip(1).SelectMany(x => x.Inputs))
                            {
                                var prevInputTxHash = prevInput.PreviousTxOutputKey.TxHash;
                                if (!prevInputTxes.ContainsKey(prevInputTxHash))
                                    prevInputTxes.Add(prevInputTxHash, this.CacheContext.TransactionCache[prevInputTxHash]);
                            }
                        });

                        return Tuple.Create(chainedBlockDirection, chainedBlock, block, prevInputTxes.ToImmutable());
                    })
                .LookAhead(maxLookAhead, this.shutdownToken);
        }

        private UInt256 CalculateHash(BlockHeader blockHeader)
        {
            return new UInt256(Crypto.DoubleSHA256(DataCalculator.EncodeBlockHeader(blockHeader)));
        }
    }
}
