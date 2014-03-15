using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain.ExtensionMethods;
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

        public void CalculateBlockchainFromExisting(BlockchainBuilder blockchainBuilder, BlockchainPathBuilder targetBlockPathBuilder, out List<MissingDataException> missingData, CancellationToken cancelToken, Action onProgress = null)
        {
            missingData = new List<MissingDataException>();

            blockchainBuilder.Stats.totalStopwatch.Start();
            blockchainBuilder.Stats.currentRateStopwatch.Start();

            // calculate the new blockchain along the target path
            bool utxoSafe = true;
            try
            {
                //TODO make this work with look-ahead again
                //foreach (var tuple in BlockAndTxLookAhead(targetBlockPathBuilder.RewindBlocks.Select(x => x.BlockHash).ToList()))
                Tuple<int, ChainedBlock> pathElement;
                while ((pathElement = targetBlockPathBuilder.PopFromBlock()) != null)
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
                    //TODO make this work with look-ahead again
                    var block = this.CacheContext.GetBlock(chainedBlock.BlockHash);

                    if (direction < 0)
                    {
                        List<TxOutputKey> spendOutputs, receiveOutputs;
                        RollbackUtxo(blockchainBuilder, block, out spendOutputs, out receiveOutputs);

                        blockchainBuilder.BlockList.RemoveAt(blockchainBuilder.BlockList.Count - 1);
                        blockchainBuilder.BlockListHashes.Remove(blockchainBuilder.RootBlockHash);
                    }
                    else if (direction > 0)
                    {
                        // calculate the new block utxo, double spends will be checked for
                        ImmutableDictionary<UInt256, ImmutableHashSet<int>> newTransactions = ImmutableDictionary.Create<UInt256, ImmutableHashSet<int>>();
                        long txCount = 0, inputCount = 0;
                        new MethodTimer(false).Time("CalculateUtxo", () =>
                            CalculateUtxo(chainedBlock.Height, block, blockchainBuilder.UtxoBuilder, out newTransactions, out txCount, out inputCount));

                        blockchainBuilder.BlockList.Add(chainedBlock);
                        blockchainBuilder.BlockListHashes.Add(chainedBlock.BlockHash);

                        // validate the block
                        // validation utxo includes all transactions added in the same block, any double spends will have failed the block above
                        blockchainBuilder.Stats.validateStopwatch.Start();
                        try
                        {
                            new MethodTimer(false).Time("ValidateBlock", () =>
                                this.Rules.ValidateBlock(block, blockchainBuilder, newTransactions));
                        }
                        finally
                        {
                            blockchainBuilder.Stats.validateStopwatch.Stop();
                        }

                        // flush utxo progress
                        blockchainBuilder.UtxoBuilder.Flush();

                        // create the next link in the new blockchain
                        if (onProgress != null)
                            onProgress();

                        // blockchain processing statistics
                        blockchainBuilder.Stats.currentBlockCount++;
                        blockchainBuilder.Stats.currentTxCount += txCount;
                        blockchainBuilder.Stats.currentInputCount += inputCount;
                        blockchainBuilder.Stats.totalTxCount += txCount;
                        blockchainBuilder.Stats.totalInputCount += inputCount;

                        var txInterval = 100.THOUSAND();
                        if (
                            blockchainBuilder.Height % 10.THOUSAND() == 0
                            || (blockchainBuilder.Stats.totalTxCount % txInterval < (blockchainBuilder.Stats.totalTxCount - txCount) % txInterval || txCount >= txInterval))
                        {
                            LogBlockchainProgress(blockchainBuilder);

                            blockchainBuilder.Stats.currentBlockCount = 0;
                            blockchainBuilder.Stats.currentTxCount = 0;
                            blockchainBuilder.Stats.currentInputCount = 0;
                            blockchainBuilder.Stats.currentRateStopwatch.Reset();
                            blockchainBuilder.Stats.currentRateStopwatch.Start();
                        }
                    }
                    else
                        throw new InvalidOperationException();
                    utxoSafe = true;
                }
            }
            catch (MissingDataException e)
            {
                // if there is missing data once blockchain processing has started, return the current progress
                missingData.Add(e);
                if (!utxoSafe)
                    throw;
            }
            catch (AggregateException e)
            {
                if (e.InnerExceptions.Any(x => !(x is MissingDataException)))
                {
                    throw;
                }
                else
                {
                    missingData.AddRange(e.InnerExceptions.OfType<MissingDataException>());
                    if (!utxoSafe)
                        throw;
                }
            }

            if (onProgress != null)
                onProgress();

            LogBlockchainProgress(blockchainBuilder);
            blockchainBuilder.Stats.totalStopwatch.Stop();
            blockchainBuilder.Stats.currentRateStopwatch.Stop();
        }

        public void RollbackToLastCommonAncestor(BlockchainBuilder blockchainBuilder, ChainedBlock targetChainedBlock, IImmutableList<ChainedBlock> currentChain, CancellationToken cancelToken, out List<UInt256> newChainBlockList)
        {
            // take snapshots
            var newChainedBlock = targetChainedBlock;
            newChainBlockList = new List<UInt256>();

            // check height difference between chains, they will be roll backed before checking for the last common ancestor
            var heightDelta = targetChainedBlock.Height - blockchainBuilder.Height;

            // if current chain is shorter, roll new chain back to current chain's height
            if (heightDelta > 0)
            {
                List<ChainedBlock> rolledBackChainedBlocks;
                newChainedBlock = RollbackChainedBlockToHeight(targetChainedBlock, blockchainBuilder.Height, currentChain, out rolledBackChainedBlocks, this.shutdownToken);
                newChainBlockList.AddRange(rolledBackChainedBlocks.Select(x => x.BlockHash));
            }
            // if current chain is longer, roll it back to new chain's height
            else if (heightDelta < 0)
            {
                RollbackBlockchainToHeight(blockchainBuilder, newChainedBlock.Height, currentChain, this.shutdownToken);
            }

            if (newChainedBlock.Height != blockchainBuilder.Height)
                throw new Exception();

            //TODO continue looking backwards while processing moves forward to double check
            //TODO the blockchain history back to genesis? only look at height, work, valid bits in
            //TODO the metadata, sync and check this task at the end before updating current blockchain,
            //TODO if any error is ever found, mark everything after it as invalid or unprocessed, the
            //TODO processor could get stuck otherwise trying what it thinks is the winning chain over and over

            // with both chains at the same height, roll back to last common ancestor
            if (newChainedBlock.BlockHash != blockchainBuilder.RootBlockHash)
            {
                var rollbackList = new List<UInt256>();
                var currentBlockchainIndex = blockchainBuilder.BlockList.Count - 1;
                foreach (var prevBlock in PreviousChainedBlocks(newChainedBlock, currentChain))
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    newChainedBlock = prevBlock;
                    if (newChainedBlock.BlockHash == blockchainBuilder.BlockList[currentBlockchainIndex].BlockHash)
                    {
                        break;
                    }

                    // ensure that height is as expected while looking up previous blocks
                    if (newChainedBlock.Height != blockchainBuilder.BlockList[currentBlockchainIndex].Height)
                    {
                        throw new ValidationException();
                    }

                    // keep track of rolled back data on the new blockchain
                    newChainBlockList.Add(newChainedBlock.BlockHash);

                    // queue up current blockchain rollback
                    rollbackList.Add(blockchainBuilder.BlockList[currentBlockchainIndex].BlockHash);

                    currentBlockchainIndex--;
                }

                // roll back current block chain
                foreach (var tuple in BlockLookAhead(rollbackList))
                {
                    var block = tuple.Item1;
                    RollbackBlockchain(blockchainBuilder, block);
                }
            }

            // work list will have last items added first, reverse
            newChainBlockList.Reverse();
        }

        public List<ChainedBlock> FindBlocksPastLastCommonAncestor(Data.Blockchain currentBlockchain, ChainedBlock targetChainedBlock, IImmutableList<ChainedBlock> currentChain, CancellationToken cancelToken, out ImmutableList<ChainedBlock> rolledBackBlocks)
        {
            //ImmutableList<ChainedBlock> rolledBackBlocksLocal;
            //var result = new MethodTimer().Time(() =>
            //{
            // take snapshots
            var newChainedBlock = targetChainedBlock;
            var newChainBlockList = new List<ChainedBlock>();

            // check height difference between chains, they will be roll backed before checking for the last common ancestor
            var heightDelta = targetChainedBlock.Height - currentBlockchain.Height;

            var rolledBackBlocksBuilder = ImmutableList.CreateBuilder<ChainedBlock>();

            // if current chain is shorter, roll new chain back to current chain's height
            ImmutableList<ChainedBlock> currentChainedBlocks;
            if (heightDelta > 0)
            {
                currentChainedBlocks = currentBlockchain.BlockList;

                List<ChainedBlock> rolledBackChainedBlocks;
                newChainedBlock = RollbackChainedBlockToHeight(targetChainedBlock, currentBlockchain.Height, currentChain, out rolledBackChainedBlocks, this.shutdownToken);
                newChainBlockList.AddRange(rolledBackChainedBlocks);
            }
            // if current chain is longer, roll it back to new chain's height
            else if (heightDelta < 0)
            {
                rolledBackBlocksBuilder.InsertRange(0, currentBlockchain.BlockList.GetRange(targetChainedBlock.Height + 1, currentBlockchain.BlockList.Count));
                currentChainedBlocks = currentBlockchain.BlockList.GetRange(0, targetChainedBlock.Height + 1);
            }
            else
            {
                currentChainedBlocks = currentBlockchain.BlockList;
            }

            if (newChainedBlock.Height != currentChainedBlocks.Last().Height)
                throw new Exception();

            // with both chains at the same height, roll back to last common ancestor
            if (newChainedBlock.BlockHash != currentChainedBlocks.Last().BlockHash)
            {
                foreach (var tuple in
                    PreviousChainedBlocks(newChainedBlock, currentChain).Zip(currentChainedBlocks.Reverse<ChainedBlock>(),
                        (prevBlock, currentBlock) => Tuple.Create(prevBlock, currentBlock)))
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();
                    cancelToken.ThrowIfCancellationRequested();

                    newChainedBlock = tuple.Item1;
                    var currentBlock = tuple.Item2;
                    rolledBackBlocksBuilder.Add(currentBlock);

                    // ensure that height is as expected while looking up previous blocks
                    if (newChainedBlock.Height != currentBlock.Height)
                    {
                        throw new ValidationException();
                    }

                    if (newChainedBlock.BlockHash == currentBlock.BlockHash)
                    {
                        break;
                    }

                    // keep track of rolled back data on the new blockchain
                    newChainBlockList.Add(newChainedBlock);
                }
            }

            // work list will have last items added first, reverse
            newChainBlockList.Reverse();

            rolledBackBlocks = rolledBackBlocksBuilder.ToImmutable();

            return newChainBlockList;
            //});
        }

        public ChainedBlock RollbackChainedBlockToHeight(ChainedBlock chainedBlock, int targetHeight, IImmutableList<ChainedBlock> currentChain, out List<ChainedBlock> rolledBackChainedBlocks, CancellationToken cancelToken)
        {
            if (targetHeight > chainedBlock.Height || targetHeight < 0)
                throw new ArgumentOutOfRangeException("targetHeight");

            rolledBackChainedBlocks = new List<ChainedBlock>();

            var targetChainedBlock = chainedBlock;
            var expectedHeight = targetChainedBlock.Height;
            while (targetChainedBlock.Height > targetHeight)
            {
                // cooperative loop
                cancelToken.ThrowIfCancellationRequested();

                // keep track of rolled back data on the new blockchain
                rolledBackChainedBlocks.Add(targetChainedBlock);

                // roll back
                if (currentChain != null && currentChain.Count > targetChainedBlock.Height && currentChain[targetChainedBlock.Height - 1].BlockHash == targetChainedBlock.PreviousBlockHash)
                {
                    targetChainedBlock = currentChain[targetChainedBlock.Height - 1];
                }
                else
                {
                    targetChainedBlock = this.CacheContext.GetChainedBlock(targetChainedBlock.PreviousBlockHash);
                }

                // ensure that height is as expected while looking up previous blocks
                expectedHeight--;
                if (targetChainedBlock.Height != expectedHeight)
                    throw new ValidationException();
            }

            return targetChainedBlock;
        }

        public void RollbackBlockchainToHeight(BlockchainBuilder blockchainBuilder, int targetHeight, IImmutableList<ChainedBlock> currentChain, CancellationToken cancelToken)
        {
            if (targetHeight > blockchainBuilder.Height || targetHeight < 0)
                throw new ArgumentOutOfRangeException("targetHeight");

            List<ChainedBlock> rolledBackChainedBlocks;
            var targetChainedBlock = RollbackChainedBlockToHeight(blockchainBuilder.RootBlock, targetHeight, currentChain, out rolledBackChainedBlocks, cancelToken);

            var rollbackCount = blockchainBuilder.Height - targetHeight;
            if (rolledBackChainedBlocks.Count != rollbackCount)
                throw new Exception();

            var rollbackIndex = 0;
            foreach (var tuple in BlockLookAhead(rolledBackChainedBlocks.Select(x => x.BlockHash).ToList()))
            {
                // cooperative loop
                this.shutdownToken.ThrowIfCancellationRequested();
                cancelToken.ThrowIfCancellationRequested();

                // roll back
                var block = tuple.Item1;
                Debug.Assert(blockchainBuilder.RootBlockHash == block.Hash);
                RollbackBlockchain(blockchainBuilder, block);

                Debug.WriteLineIf(rollbackIndex % 100 == 0, "Rolling back {0} of {1}".Format2(rollbackIndex + 1, rollbackCount));
                rollbackIndex++;
            }
        }

        public void RollbackBlockchain(BlockchainBuilder blockchainBuilder, Block block)
        {
            List<TxOutputKey> spendOutputs, receiveOutputs;
            RollbackBlockchain(blockchainBuilder, block, out spendOutputs, out receiveOutputs);
        }

        public void RollbackBlockchain(BlockchainBuilder blockchainBuilder, Block block, out List<TxOutputKey> spendOutputs, out List<TxOutputKey> receiveOutputs)
        {
            if (blockchainBuilder.BlockCount == 0 || blockchainBuilder.RootBlockHash != block.Hash)
                throw new ValidationException();

            RollbackUtxo(blockchainBuilder, block, out spendOutputs, out receiveOutputs);

            blockchainBuilder.BlockList.RemoveAt(blockchainBuilder.BlockList.Count - 1);
            blockchainBuilder.BlockListHashes.Remove(blockchainBuilder.RootBlockHash);
        }

        private void LogBlockchainProgress(BlockchainBuilder blockchainBuilder)
        {
            var currentBlockRate = (float)blockchainBuilder.Stats.currentBlockCount / blockchainBuilder.Stats.currentRateStopwatch.ElapsedSecondsFloat();
            var currentTxRate = (float)blockchainBuilder.Stats.currentTxCount / blockchainBuilder.Stats.currentRateStopwatch.ElapsedSecondsFloat();
            var currentInputRate = (float)blockchainBuilder.Stats.currentInputCount / blockchainBuilder.Stats.currentRateStopwatch.ElapsedSecondsFloat();

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
                /*0*/ blockchainBuilder.Height.ToString("#,##0"),
                /*1*/ blockchainBuilder.Stats.totalStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*2*/ blockchainBuilder.Stats.validateStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*3*/ currentBlockRate.ToString("#,##0"),
                /*4*/ currentTxRate.ToString("#,##0"),
                /*5*/ currentInputRate.ToString("#,##0"),
                /*6*/ blockchainBuilder.Stats.totalTxCount.ToString("#,##0"),
                /*7*/ blockchainBuilder.Stats.totalInputCount.ToString("#,##0"),
                /*8*/ blockchainBuilder.UtxoBuilder.Count.ToString("#,##0"),
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

        private void RollbackUtxo(BlockchainBuilder blockchainBuilder, Block block, out List<TxOutputKey> spendOutputs, out List<TxOutputKey> receiveOutputs)
        {
            var blockHeight = blockchainBuilder.Height;
            var utxoBuilder = blockchainBuilder.UtxoBuilder;

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
                        var prevUnspentTx = this.CacheContext.GetTransaction(input.PreviousTxOutputKey.TxHash);

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

        public void RevalidateBlockchain(Data.Blockchain blockchain, Block genesisBlock)
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
                var chainGenesisBlockHeader = this.CacheContext.GetBlockHeader(blockchain.BlockList[0].BlockHash);

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
                    var blockHeader = this.CacheContext.GetBlockHeader(chainedBlock.BlockHash);

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

        public IEnumerable<Tuple<Block, ChainedBlock>> BlockLookAhead(IList<UInt256> blockHashes)
        {
            var blockLookAhead = LookAheadMethods.LookAhead(
                blockHashes.Select(blockHash => this.CacheContext.GetBlock(blockHash, saveInCache: false)),
                this.shutdownToken);

            var chainedBlockLookAhead = LookAheadMethods.LookAhead(
                blockHashes.Select(blockHash => this.CacheContext.GetChainedBlock(blockHash, saveInCache: false)),
                this.shutdownToken);

            return blockLookAhead.Zip(chainedBlockLookAhead, (block, chainedBlock) => Tuple.Create(block, chainedBlock));
        }

        public IEnumerable<Tuple<Block, ChainedBlock /*, ImmutableDictionary<UInt256, Transaction>*/>> BlockAndTxLookAhead(IList<UInt256> blockHashes)
        {
            var blockLookAhead = LookAheadMethods.LookAhead(
                blockHashes.Select(
                    blockHash =>
                    {
                        var block = new MethodTimer(false).Time("GetBlock", () =>
                            this.CacheContext.GetBlock(blockHash, saveInCache: false));

                        this.CacheContext.TransactionCache.CacheBlock(block);

                        //var transactionsBuilder = ImmutableDictionary.CreateBuilder<UInt256, Transaction>();
                        //var inputTxHashList = block.Transactions.Skip(1).SelectMany(x => x.Inputs).Select(x => x.PreviousTxOutputKey.TxHash).Distinct();

                        //// pre-cache input transactions
                        ////Parallel.ForEach(inputTxHashList, inputTxHash =>
                        //foreach (var inputTxHash in inputTxHashList)
                        //{
                        //    Transaction inputTx;
                        //    if (this.CacheContext.TransactionCache.TryGetValue(inputTxHash, out inputTx, saveInCache: false))
                        //    {
                        //        transactionsBuilder.Add(inputTxHash, inputTx);
                        //    }
                        //}

                        //return Tuple.Create(block, transactionsBuilder.ToImmutable());
                        return block;
                    }),
                this.shutdownToken);

            var chainedBlockLookAhead = LookAheadMethods.LookAhead(
                blockHashes.Select(blockHash => this.CacheContext.GetChainedBlock(blockHash, saveInCache: false)),
                this.shutdownToken);

            return blockLookAhead.Zip(chainedBlockLookAhead, (block, chainedBlock) => Tuple.Create(block, chainedBlock));
        }

        public IEnumerable<Tuple<ChainedBlock, Block>> PreviousBlocksLookAhead(ChainedBlock firstBlock, IImmutableList<ChainedBlock> currentChain)
        {
            using (var cancelToken = new CancellationTokenSource())
            {
                foreach (var tuple in LookAheadMethods.LookAhead(PreviousBlocks(firstBlock, currentChain), cancelToken.Token))
                {
                    yield return tuple;
                }
            }
        }


        public IEnumerable<Tuple<ChainedBlock, Block>> PreviousBlocks(ChainedBlock firstBlock, IImmutableList<ChainedBlock> currentChain)
        {
            var prevChainedBlock = firstBlock;
            //TODO some kind of hard stop
            while (true)
            {
                var prevBlock = this.CacheContext.GetBlock(prevChainedBlock.BlockHash);

                yield return Tuple.Create(prevChainedBlock, prevBlock);

                var prevBlockHash = prevChainedBlock.PreviousBlockHash;
                if (prevBlockHash == 0)
                {
                    break;
                }

                prevChainedBlock = this.CacheContext.GetChainedBlock(prevBlockHash);
            }
        }

        public IEnumerable<ChainedBlock> PreviousChainedBlocks(ChainedBlock firstBlock, IImmutableList<ChainedBlock> currentChain)
        {
            var prevChainedBlock = firstBlock;
            //TODO some kind of hard stop
            while (true)
            {
                yield return prevChainedBlock;

                var prevBlockHash = prevChainedBlock.PreviousBlockHash;
                if (prevBlockHash == 0)
                {
                    break;
                }

                if (currentChain != null && currentChain.Count > prevChainedBlock.Height && currentChain[prevChainedBlock.Height - 1].BlockHash == prevBlockHash)
                {
                    prevChainedBlock = currentChain[prevChainedBlock.Height - 1];
                }
                else
                {
                    prevChainedBlock = this.CacheContext.GetChainedBlock(prevBlockHash);
                }
            }
        }

        private UInt256 CalculateHash(BlockHeader blockHeader)
        {
            return new UInt256(Crypto.DoubleSHA256(DataCalculator.EncodeBlockHeader(blockHeader)));
        }
    }
}
