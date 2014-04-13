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
using System.IO;
using System.Globalization;
using System.Collections;
using NLog;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Workers;
using BitSharp.Core.Wallet;
using System.Security.Cryptography;
using BitSharp.Core.Domain;

namespace BitSharp.Core.Builders
{
    public class ChainStateBuilder : IDisposable
    {
        private readonly Logger logger;
        private readonly CancellationToken shutdownToken;
        private readonly IBlockchainRules rules;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly BlockCache blockCache;
        private readonly SpentTransactionsCache spentTransactionsCache;
        private readonly SpentOutputsCache spentOutputsCache;

        private readonly ChainBuilder chain;
        private readonly UtxoBuilder utxo;
        private readonly BuilderStats stats;

        public ChainStateBuilder(ChainBuilder chain, UtxoBuilder utxo, CancellationToken shutdownToken, Logger logger, IBlockchainRules rules, BlockHeaderCache blockHeaderCache, BlockCache blockCache, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
        {
            this.logger = logger;
            this.shutdownToken = shutdownToken;
            this.rules = rules;
            this.blockHeaderCache = blockHeaderCache;
            this.blockCache = blockCache;
            this.spentTransactionsCache = spentTransactionsCache;
            this.spentOutputsCache = spentOutputsCache;

            this.chain = chain;
            this.utxo = utxo;
            this.stats = new BuilderStats();
            this.stats.durationStopwatch.Start();

            this.IsConsistent = true;
        }

        ~ChainStateBuilder()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            this.utxo.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool IsConsistent { get; private set; }

        public ChainBuilder Chain { get { return this.chain; } }

        public UtxoBuilder Utxo { get { return this.utxo; } }

        public ChainedBlock LastBlock { get { return this.chain.LastBlock; } }

        public UInt256 LastBlockHash
        {
            get
            {
                var lastBlockLocal = this.LastBlock;
                if (lastBlockLocal != null)
                    return this.LastBlock.BlockHash;
                else
                    return UInt256.Zero;
            }
        }

        public int Height
        {
            get
            {
                var lastBlockLocal = this.LastBlock;
                if (lastBlockLocal != null)
                    return this.LastBlock.Height;
                else
                    return -1;
            }
        }

        public BuilderStats Stats { get { return this.stats; } }

        public void CalculateBlockchainFromExisting(Func<Chain> getTargetChain, Func<IImmutableSet<ITransactionMonitor>> getTxMonitors, CancellationToken cancelToken, Action<TimeSpan> onProgress = null)
        {
            //this.Stats.totalStopwatch.Start();
            //this.Stats.currentRateStopwatch.Start();

            // calculate the new blockchain along the target path
            this.IsConsistent = true;
            foreach (var pathElement in BlockLookAhead(this.Chain.NavigateTowards(getTargetChain), lookAhead: 1))
            {
                this.IsConsistent = false;
                var startTime = DateTime.UtcNow;

                // cooperative loop
                if (this.shutdownToken.IsCancellationRequested)
                    break;
                if (cancelToken.IsCancellationRequested)
                    break;

                // get block and metadata for next link in blockchain
                var direction = pathElement.Item1;
                var chainedBlock = pathElement.Item2;
                var block = pathElement.Item3;

                if (direction < 0)
                {
                    this.RollbackUtxo(block);

                    this.Chain.RemoveBlock(chainedBlock);
                }
                else if (direction > 0)
                {
                    // add the block to the current chain
                    this.Chain.AddBlock(chainedBlock);

                    // validate the block
                    this.Stats.validateStopwatch.Start();
                    new MethodTimer(false).Time("ValidateBlock", () =>
                        this.rules.ValidateBlock(block, this));
                    this.Stats.validateStopwatch.Stop();

                    // calculate the new block utxo, double spends will be checked for
                    long txCount = 0, inputCount = 0;
                    new MethodTimer(false).Time("CalculateUtxo", () =>
                        CalculateUtxo(chainedBlock, block, this.Utxo, getTxMonitors, out txCount, out inputCount));

                    // collect rollback informatino and store it
                    this.Utxo.SaveRollbackInformation(chainedBlock.Height, block.Hash, this.spentTransactionsCache, this.spentOutputsCache);

                    // flush utxo progress
                    //this.Utxo.Flush();

                    // create the next link in the new blockchain
                    if (onProgress != null)
                        onProgress(DateTime.UtcNow - startTime);

                    // blockchain processing statistics
                    this.Stats.blockCount++;
                    this.Stats.txCount += txCount;
                    this.Stats.inputCount += inputCount;

                    var txInterval = TimeSpan.FromSeconds(15);
                    if (DateTime.UtcNow - this.Stats.lastLogTime >= txInterval)
                    {
                        this.LogBlockchainProgress();
                        this.Stats.lastLogTime = DateTime.UtcNow;
                    }
                }
                else
                    throw new InvalidOperationException();

                this.IsConsistent = true;
            }
        }

        public void LogBlockchainProgress()
        {
            var elapsedSeconds = this.Stats.durationStopwatch.ElapsedSecondsFloat();
            var blockRate = (float)this.Stats.blockCount / elapsedSeconds;
            var txRate = (float)this.Stats.txCount / elapsedSeconds;
            var inputRate = (float)this.Stats.inputCount / elapsedSeconds;

            this.logger.Info(
                string.Join("\n",
                    new string('-', 200),
                    "Height: {0,10} | Duration: {1} /*| Validation: {2} */| Blocks/s: {3,7} | Tx/s: {4,7} | Inputs/s: {5,7} | Processed Tx: {6,7} | Processed Inputs: {7,7} | Utxo Size: {8,7}",
                    new string('-', 200)
                )
                .Format2
                (
                /*0*/ this.Height.ToString("#,##0"),
                /*1*/ this.Stats.durationStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*2*/ this.Stats.validateStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*3*/ blockRate.ToString("#,##0"),
                /*4*/ txRate.ToString("#,##0"),
                /*5*/ inputRate.ToString("#,##0"),
                /*6*/ this.Stats.txCount.ToString("#,##0"),
                /*7*/ this.Stats.inputCount.ToString("#,##0"),
                /*8*/ this.Utxo.OutputCount.ToString("#,##0")
                ));
        }

        private void CalculateUtxo(ChainedBlock chainedBlock, Block block, UtxoBuilder utxoBuilder, Func<IImmutableSet<ITransactionMonitor>> getTxMonitors, out long txCount, out long inputCount)
        {
            txCount = 1;
            inputCount = 0;

            var txMonitors = getTxMonitors().ToArray();
            using (var txInputQueue = new ProducerConsumer<Tuple<Transaction, int, TxInput, int, TxOutput>>())
            using (var validateScriptsTask = Task.Factory.StartNew(() => this.ValidateTransactionScripts(block, txInputQueue)))
            using (var txOutputQueue = new ProducerConsumer<Tuple<int, ChainPosition, TxOutput>>())
            using (var scannerTask = Task.Factory.StartNew(() => this.ScanTransactions(txOutputQueue, txMonitors)))
            {
                // don't include genesis block coinbase in utxo
                if (chainedBlock.Height > 0)
                {
                    //TODO apply real coinbase rule
                    // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                    var coinbaseTx = block.Transactions[0];

                    utxoBuilder.Mint(coinbaseTx, chainedBlock);

                    foreach (var output in coinbaseTx.Outputs)
                        txOutputQueue.Add(Tuple.Create<int, ChainPosition, TxOutput>(0, new ChainPosition(0, 0, 0, 0), output));
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

                        var spentOutput = utxoBuilder.Spend(input, chainedBlock);

                        txInputQueue.Add(Tuple.Create<Transaction, int, TxInput, int, TxOutput>(tx, txIndex, input, inputIndex, spentOutput));
                        txOutputQueue.Add(Tuple.Create<int, ChainPosition, TxOutput>(-1, new ChainPosition(0, 0, 0, 0), spentOutput));
                    }

                    utxoBuilder.Mint(tx, chainedBlock);

                    foreach (var output in tx.Outputs)
                        txOutputQueue.Add(Tuple.Create<int, ChainPosition, TxOutput>(+1, new ChainPosition(0, 0, 0, 0), output));
                }

                // wait for scanner to finish
                txInputQueue.CompleteAdding();
                txOutputQueue.CompleteAdding();

                validateScriptsTask.Wait();
                scannerTask.Wait();

                Debug.Assert(txInputQueue.IsCompleted);
                Debug.Assert(txOutputQueue.IsCompleted);
            }
        }

        private void ValidateTransactionScripts(Block block, ProducerConsumer<Tuple<Transaction, int, TxInput, int, TxOutput>> txInputQueue)
        {
            var exceptions = new ConcurrentBag<Exception>();

            Parallel.ForEach(
                txInputQueue.GetConsumingEnumerable(),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                (tuple, loopState) =>
                {
                    try
                    {
                        this.rules.ValidationTransactionScript(block, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                        if (!MainnetRules.IgnoreScriptErrors)
                            loopState.Stop();
                    }
                });

            if (exceptions.Count > 0)
            {
                if (!MainnetRules.IgnoreScriptErrors)
                    throw new AggregateException(exceptions.ToArray());
                else
                    this.logger.Debug("Ignoring script error in block: {0}".Format2(block.Hash));
            }
        }

        private void ScanTransactions(ProducerConsumer<Tuple<int, ChainPosition, TxOutput>> txOutputQueue, ITransactionMonitor[] txMonitors)
        {
                var sha256 = new SHA256Managed();
            //TODO for this to remain parallel, i'll need to be able to handle the case of a new watch address being generated from one of the iterations
            //TODO there will also need to be an ordering step, so i'll have to evaluate if parallel is best here
            //Parallel.ForEach(
            //    txOutputQueue.GetConsumingEnumerable(),
            //    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
            //    txOutput =>
            foreach (var txOutput in txOutputQueue.GetConsumingEnumerable())
            {
                var outputScriptHash = new UInt256(sha256.ComputeHash(txOutput.Item3.ScriptPublicKey.ToArray()));

                for (var i = 0; i < txMonitors.Length; i++)
                {
                    var txMonitor = txMonitors[i];
                    if (txOutput.Item1 < 0)
                        txMonitor.SpendTxOutput(txOutput.Item2, txOutput.Item3, outputScriptHash);
                    else if (txOutput.Item1 > 0)
                        txMonitor.ReceiveTxOutput(txOutput.Item2, txOutput.Item3, outputScriptHash);
                    else
                        txMonitor.MintTxOutput(txOutput.Item2, txOutput.Item3, outputScriptHash);
                }
            }//);
        }

        //TODO with the rollback information that's now being stored, rollback could be down without needing the block
        private void RollbackUtxo(Block block)
        {
            //TODO currently a MissingDataException will get thrown if the rollback information is missing
            //TODO rollback is still possible if any resurrecting transactions can be found
            //TODO the network does not allow arbitrary transaction lookup, but if the transactions can be retrieved then this code should allow it
            //TODO this should be handle by a distinct worker that rebuilds rollback information

            var spentTransactions = new Dictionary<UInt256, SpentTx>();
            spentTransactions.AddRange(this.spentTransactionsCache[block.Hash]);

            var spentOutputs = new Dictionary<TxOutputKey, TxOutput>();
            spentOutputs.AddRange(this.spentOutputsCache[block.Hash]);

            for (var txIndex = block.Transactions.Count - 1; txIndex >= 1; txIndex--)
            {
                var tx = block.Transactions[txIndex];

                // remove outputs
                this.Utxo.Unmint(tx, this.LastBlock);

                // remove inputs in reverse order
                for (var inputIndex = tx.Inputs.Count - 1; inputIndex >= 0; inputIndex--)
                {
                    var input = tx.Inputs[inputIndex];
                    this.Utxo.Unspend(input, this.LastBlock, spentTransactions, spentOutputs);
                }
            }

            // remove coinbase outputs
            var coinbaseTx = block.Transactions[0];
            this.Utxo.Unmint(coinbaseTx, this.LastBlock);
        }

        private void RevalidateBlockchain(Chain blockchain, Block genesisBlock)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                //TODO delete corrupted data? could get stuck in a fail-loop on the winning chain otherwise

                // verify blockchain has blocks
                if (blockchain.Blocks.Count == 0)
                    throw new ValidationException(0);

                // verify genesis block hash
                if (blockchain.Blocks[0].BlockHash != genesisBlock.Hash)
                    throw new ValidationException(blockchain.Blocks[0].BlockHash);

                // get genesis block header
                var chainGenesisBlockHeader = this.blockHeaderCache[blockchain.Blocks[0].BlockHash];

                // verify genesis block header
                if (
                    genesisBlock.Header.Version != chainGenesisBlockHeader.Version
                    || genesisBlock.Header.PreviousBlock != chainGenesisBlockHeader.PreviousBlock
                    || genesisBlock.Header.MerkleRoot != chainGenesisBlockHeader.MerkleRoot
                    || genesisBlock.Header.Time != chainGenesisBlockHeader.Time
                    || genesisBlock.Header.Bits != chainGenesisBlockHeader.Bits
                    || genesisBlock.Header.Nonce != chainGenesisBlockHeader.Nonce
                    || genesisBlock.Hash != chainGenesisBlockHeader.Hash
                    || genesisBlock.Hash != DataCalculator.CalculateBlockHash(chainGenesisBlockHeader))
                {
                    throw new ValidationException(chainGenesisBlockHeader.Hash);
                }

                // setup expected previous block hash value to verify each chain actually does link
                var expectedPreviousBlockHash = genesisBlock.Header.PreviousBlock;
                for (var height = 0; height < blockchain.Blocks.Count; height++)
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();

                    // get the current link in the chain
                    var chainedBlock = blockchain.Blocks[height];

                    // verify height
                    if (chainedBlock.Height != height)
                        throw new ValidationException(chainedBlock.BlockHash);

                    // verify blockchain linking
                    if (chainedBlock.PreviousBlockHash != expectedPreviousBlockHash)
                        throw new ValidationException(chainedBlock.BlockHash);

                    // verify block exists
                    var blockHeader = this.blockHeaderCache[chainedBlock.BlockHash];

                    // verify block metadata matches header values
                    if (blockHeader.PreviousBlock != chainedBlock.PreviousBlockHash)
                        throw new ValidationException(chainedBlock.BlockHash);

                    // verify block header hash
                    if (DataCalculator.CalculateBlockHash(blockHeader) != chainedBlock.BlockHash)
                        throw new ValidationException(chainedBlock.BlockHash);

                    // next block metadata should have the current metadata's hash as its previous hash value
                    expectedPreviousBlockHash = chainedBlock.BlockHash;
                }

                // all validation passed
            }
            finally
            {
                stopwatch.Stop();
                this.logger.Info("Blockchain revalidation: {0:#,##0.000000}s".Format2(stopwatch.ElapsedSecondsFloat()));
            }
        }

        private IEnumerable<Tuple<int, ChainedBlock, Block>> BlockLookAhead(IEnumerable<Tuple<int, ChainedBlock>> chain, int lookAhead)
        {
            return chain
                .Select(
                    chainedBlockTuple =>
                    {
                        try
                        {
                            var chainedBlockDirection = chainedBlockTuple.Item1;
                            var chainedBlock = chainedBlockTuple.Item2;

                            var block = new MethodTimer(false).Time("GetBlock", () =>
                                this.blockCache[chainedBlock.BlockHash]);

                            return Tuple.Create(chainedBlockDirection, chainedBlock, block);
                        }
                        catch (MissingDataException e)
                        {
                            this.logger.Debug("Stalled, MissingDataException: {0}".Format2(e.Key));
                            throw;
                        }
                    })
                .LookAhead(lookAhead, this.shutdownToken);
        }

        public sealed class BuilderStats
        {
            public Stopwatch durationStopwatch = new Stopwatch();
            public Stopwatch validateStopwatch = new Stopwatch();

            public long blockCount;
            public long txCount;
            public long inputCount;

            public DateTime lastLogTime = DateTime.UtcNow;

            internal BuilderStats() { }
        }
    }
}