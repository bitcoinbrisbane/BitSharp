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
using BitSharp.Core.Monitor;
using BitSharp.Domain;
using Ninject;
using Ninject.Parameters;

namespace BitSharp.Core.Builders
{
    public class ChainStateBuilder : IDisposable
    {
        private readonly Func<Chain> getTargetChain;
        private readonly Func<IImmutableSet<IChainStateMonitor>> getMonitors;

        private readonly Logger logger;
        private readonly CancellationToken shutdownToken;
        private readonly IBlockchainRules rules;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly BlockCache blockCache;
        private readonly SpentTransactionsCache spentTransactionsCache;
        private readonly SpentOutputsCache spentOutputsCache;

        private ChainBuilder chain;
        private Chain savedChain;
        private readonly IChainStateBuilderStorage chainStateBuilderStorage;

        //TODO when written more directly against Esent, these can be streamed out so an entire list doesn't need to be held in memory
        private readonly ImmutableList<KeyValuePair<UInt256, SpentTx>>.Builder spentTransactions;
        private readonly ImmutableList<KeyValuePair<TxOutputKey, TxOutput>>.Builder spentOutputs;

        private readonly BuilderStats stats;

        public ChainStateBuilder(Func<Chain> getTargetChain, Func<IImmutableSet<IChainStateMonitor>> getMonitors, ChainBuilder chain, Utxo parentUtxo, CancellationToken shutdownToken, Logger logger, IKernel kernel, IBlockchainRules rules, BlockHeaderCache blockHeaderCache, BlockCache blockCache, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
        {
            this.getTargetChain = getTargetChain;
            this.getMonitors = getMonitors;
            this.logger = logger;
            this.shutdownToken = shutdownToken;
            this.rules = rules;
            this.blockHeaderCache = blockHeaderCache;
            this.blockCache = blockCache;
            this.spentTransactionsCache = spentTransactionsCache;
            this.spentOutputsCache = spentOutputsCache;

            this.chain = chain;
            this.chainStateBuilderStorage = kernel.Get<IChainStateBuilderStorage>(new ConstructorArgument("parentUtxo", parentUtxo.Storage));

            this.spentTransactions = ImmutableList.CreateBuilder<KeyValuePair<UInt256, SpentTx>>();
            this.spentOutputs = ImmutableList.CreateBuilder<KeyValuePair<TxOutputKey, TxOutput>>();

            this.stats = new BuilderStats();
            this.stats.durationStopwatch.Start();
        }

        ~ChainStateBuilder()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (this.chainStateBuilderStorage != null)
                this.chainStateBuilderStorage.Dispose();
        }

        public ChainBuilder Chain { get { return this.chain; } }

        public ChainedHeader LastBlock { get { return this.chain.LastBlock; } }

        public UInt256 LastBlockHash
        {
            get
            {
                var lastBlockLocal = this.LastBlock;
                if (lastBlockLocal != null)
                    return this.LastBlock.Hash;
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

        public void CalculateBlockchainFromExisting(CancellationToken cancelToken, Action<TimeSpan> onProgress = null)
        {
            //this.Stats.totalStopwatch.Start();
            //this.Stats.currentRateStopwatch.Start();

            // calculate the new blockchain along the target path
            foreach (var pathElement in BlockLookAhead(this.Chain.NavigateTowards(this.getTargetChain), lookAhead: 1))
            {
                var startTime = DateTime.UtcNow;

                // cooperative loop
                if (this.shutdownToken.IsCancellationRequested)
                    break;
                if (cancelToken.IsCancellationRequested)
                    break;

                // get block and metadata for next link in blockchain
                var direction = pathElement.Item1;
                var chainedBlock = pathElement.Item2;

                this.BeginTransaction();
                try
                {
                    // store block hash
                    this.chainStateBuilderStorage.BlockHash = chainedBlock.Hash;

                    if (direction < 0)
                    {
                        this.RollbackUtxo(chainedBlock);

                        this.Chain.RemoveBlock(chainedBlock.ChainedHeader);
                    }
                    else if (direction > 0)
                    {
                        // add the block to the current chain
                        this.Chain.AddBlock(chainedBlock.ChainedHeader);

                        // validate the block
                        this.Stats.validateStopwatch.Start();
                        new MethodTimer(false).Time("ValidateBlock", () =>
                            this.rules.ValidateBlock(chainedBlock, this));
                        this.Stats.validateStopwatch.Stop();

                        // calculate the new block utxo, double spends will be checked for
                        long txCount = 0, inputCount = 0;
                        new MethodTimer(false).Time("CalculateUtxo", () =>
                            this.CalculateUtxo(chainedBlock, out txCount, out inputCount));

                        // collect rollback informatino and store it
                        this.SaveRollbackInformation(chainedBlock.Hash, this.spentTransactionsCache, this.spentOutputsCache);

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

                    this.CommitTransaction();
                }
                catch (Exception)
                {
                    this.RollbackTransaction();
                    throw;
                }
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
                /*8*/ this.OutputCount.ToString("#,##0")
                ));
        }

        private void CalculateUtxo(ChainedBlock chainedBlock, out long txCount, out long inputCount)
        {
            txCount = 1;
            inputCount = 0;

            var monitors = this.getMonitors().ToArray();
            using (var txInputQueue = new ProducerConsumer<Tuple<Transaction, int, TxInput, int, TxOutput>>())
            using (var validateScriptsTask = Task.Factory.StartNew(() => this.ValidateTransactionScripts(chainedBlock, txInputQueue)))
            using (var txOutputQueue = new ProducerConsumer<Tuple<int, ChainPosition, TxOutput>>())
            using (var scannerTask = Task.Factory.StartNew(() => this.ScanTransactions(txOutputQueue, monitors)))
            {
                try
                {
                    // don't include genesis block coinbase in utxo
                    if (chainedBlock.Height > 0)
                    {
                        //TODO apply real coinbase rule
                        // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                        var coinbaseTx = chainedBlock.Transactions[0];

                        this.Mint(coinbaseTx, chainedBlock.ChainedHeader);

                        foreach (var output in coinbaseTx.Outputs)
                            txOutputQueue.Add(Tuple.Create<int, ChainPosition, TxOutput>(0, new ChainPosition(0, 0, 0, 0), output));
                    }

                    // check for double spends
                    for (var txIndex = 1; txIndex < chainedBlock.Transactions.Count; txIndex++)
                    {
                        var tx = chainedBlock.Transactions[txIndex];
                        txCount++;

                        for (var inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                        {
                            var input = tx.Inputs[inputIndex];
                            inputCount++;

                            var spentOutput = this.Spend(input, chainedBlock.ChainedHeader);

                            txInputQueue.Add(Tuple.Create<Transaction, int, TxInput, int, TxOutput>(tx, txIndex, input, inputIndex, spentOutput));
                            txOutputQueue.Add(Tuple.Create<int, ChainPosition, TxOutput>(-1, new ChainPosition(0, 0, 0, 0), spentOutput));
                        }

                        this.Mint(tx, chainedBlock.ChainedHeader);

                        foreach (var output in tx.Outputs)
                            txOutputQueue.Add(Tuple.Create<int, ChainPosition, TxOutput>(+1, new ChainPosition(0, 0, 0, 0), output));
                    }
                }
                finally
                {
                    // ensure that started tasks always complete using a finally block
                    // any exceptions will be propagated by Task.WaitAll()
                    //TODO unwrap aggregation exception into something more specific
                    txInputQueue.CompleteAdding();
                    txOutputQueue.CompleteAdding();
                    Task.WaitAll(validateScriptsTask, scannerTask);
                }
            }
        }

        private void ValidateTransactionScripts(ChainedBlock chainedBlock, ProducerConsumer<Tuple<Transaction, int, TxInput, int, TxOutput>> txInputQueue)
        {
            var exceptions = new ConcurrentBag<Exception>();

            Parallel.ForEach(
                txInputQueue.GetConsumingEnumerable(),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                (tuple, loopState) =>
                {
                    try
                    {
                        this.rules.ValidationTransactionScript(chainedBlock, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
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
                    this.logger.Debug("Ignoring script error in block: {0}".Format2(chainedBlock.Hash));
            }
        }

        private void ScanTransactions(ProducerConsumer<Tuple<int, ChainPosition, TxOutput>> txOutputQueue, IChainStateMonitor[] monitors)
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

                for (var i = 0; i < monitors.Length; i++)
                {
                    var txMonitor = monitors[i];
                    if (txOutput.Item1 < 0)
                        txMonitor.SpendTxOutput(txOutput.Item2, null /*TODO txInput*/, null /*TODO txOutputKey*/, txOutput.Item3, outputScriptHash);
                    else if (txOutput.Item1 > 0)
                        txMonitor.MintTxOutput(txOutput.Item2, null /*TODO txOutputKey*/, txOutput.Item3, outputScriptHash, isCoinbase: false);
                    else
                        txMonitor.MintTxOutput(txOutput.Item2, null /*TODO txOutputKey*/, txOutput.Item3, outputScriptHash, isCoinbase: true);
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
                this.Unmint(tx, this.LastBlock);

                // remove inputs in reverse order
                for (var inputIndex = tx.Inputs.Count - 1; inputIndex >= 0; inputIndex--)
                {
                    var input = tx.Inputs[inputIndex];
                    this.Unspend(input, this.LastBlock, spentTransactions, spentOutputs);
                }
            }

            // remove coinbase outputs
            var coinbaseTx = block.Transactions[0];
            this.Unmint(coinbaseTx, this.LastBlock);
        }

        public int TransactionCount
        {
            get { return this.chainStateBuilderStorage.TransactionCount; }
        }

        public int OutputCount
        {
            get { return this.chainStateBuilderStorage.OutputCount; }
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            return this.chainStateBuilderStorage.TryGetOutput(txOutputKey, out txOutput);
        }

        public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> GetUnspentOutputs()
        {
            return this.chainStateBuilderStorage.UnspentOutputs();
        }

        public void Mint(Transaction tx, ChainedHeader chainedHeader)
        {
            // verify transaction does not already exist in utxo
            if (this.chainStateBuilderStorage.ContainsTransaction(tx.Hash))
            {
                // two specific duplicates are allowed, from before duplicates were disallowed
                if ((chainedHeader.Height == 91842 && tx.Hash == UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber))
                    || (chainedHeader.Height == 91880 && tx.Hash == UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber)))
                {
                    UnspentTx unspentTx;
                    if (!this.chainStateBuilderStorage.TryGetTransaction(tx.Hash, out unspentTx))
                        throw new Exception("TODO");

                    //TODO the inverse needs to be special cased in RollbackUtxo as well
                    for (var i = 0; i < unspentTx.OutputStates.Length; i++)
                    {
                        if (unspentTx.OutputStates[i] == OutputState.Unspent)
                        {
                            var txOutputKey = new TxOutputKey(tx.Hash, (UInt32)i);

                            TxOutput prevOutput;
                            if (!this.chainStateBuilderStorage.TryGetOutput(txOutputKey, out prevOutput))
                                throw new Exception("TODO");

                            this.chainStateBuilderStorage.RemoveOutput(txOutputKey);

                            // store rollback information, the output will need to be added back during rollback
                            this.spentOutputs.Add(new KeyValuePair<TxOutputKey, TxOutput>(txOutputKey, prevOutput));
                        }
                    }
                    this.chainStateBuilderStorage.RemoveTransaction(tx.Hash);

                    // store rollback information, the block containing the previous transaction will need to be known during rollback
                    this.spentTransactions.Add(new KeyValuePair<UInt256, SpentTx>(tx.Hash, unspentTx.ToSpent()));
                }
                else
                {
                    // duplicate transaction output
                    this.logger.Warn("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(chainedHeader.Height, chainedHeader.Hash.ToHexNumberString()));
                    throw new ValidationException(chainedHeader.Hash);
                }
            }

            // add transaction to the utxo
            this.chainStateBuilderStorage.AddTransaction(tx.Hash, new UnspentTx(chainedHeader.Hash, tx.Outputs.Count, OutputState.Unspent));

            // add transaction outputs to the utxo
            foreach (var output in tx.Outputs.Select((x, i) => new KeyValuePair<TxOutputKey, TxOutput>(new TxOutputKey(tx.Hash, (UInt32)i), x)))
                this.chainStateBuilderStorage.AddOutput(output.Key, output.Value);
        }

        public TxOutput Spend(TxInput input, ChainedHeader chainedHeader)
        {
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx)
                || !this.chainStateBuilderStorage.ContainsOutput(input.PreviousTxOutputKey))
            {
                // output wasn't present in utxo, invalid block
                throw new ValidationException(chainedHeader.Hash);
            }

            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);

            if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
            {
                // output was out of bounds
                throw new ValidationException(chainedHeader.Hash);
            }

            if (unspentTx.OutputStates[outputIndex] == OutputState.Spent)
            {
                // output was already spent
                throw new ValidationException(chainedHeader.Hash);
            }

            // update output states
            unspentTx = unspentTx.SetOutputState(outputIndex, OutputState.Spent);

            //TODO don't remove data immediately, needs to stick around for rollback

            // update partially spent transaction in the utxo
            if (unspentTx.OutputStates.Any(x => x == OutputState.Unspent))
            {
                this.chainStateBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx);
            }
            // remove fully spent transaction from the utxo
            else
            {
                this.chainStateBuilderStorage.RemoveTransaction(input.PreviousTxOutputKey.TxHash);

                // store rollback information, the block containing the previous transaction will need to be known during rollback
                this.spentTransactions.Add(new KeyValuePair<UInt256, SpentTx>(input.PreviousTxOutputKey.TxHash, unspentTx.ToSpent()));
            }

            // retrieve previous output
            TxOutput prevOutput;
            if (!this.chainStateBuilderStorage.TryGetOutput(input.PreviousTxOutputKey, out prevOutput))
                throw new Exception("TODO - corruption");

            // store rollback information, the output will need to be added back during rollback
            this.spentOutputs.Add(new KeyValuePair<TxOutputKey, TxOutput>(input.PreviousTxOutputKey, prevOutput));

            // remove the output from the utxo
            this.chainStateBuilderStorage.RemoveOutput(input.PreviousTxOutputKey);

            return prevOutput;
        }

        public void Unmint(Transaction tx, ChainedHeader chainedHeader)
        {
            // check that transaction exists
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(tx.Hash, out unspentTx))
            {
                // missing transaction output
                this.logger.Warn("Missing transaction at block {0:#,##0}, {1}, tx {2}".Format2(chainedHeader.Height, chainedHeader.Hash.ToHexNumberString(), tx.Hash));
                throw new ValidationException(chainedHeader.Hash);
            }

            //TODO verify blockheight

            // verify all outputs are unspent before unminting
            if (!unspentTx.OutputStates.All(x => x == OutputState.Unspent))
            {
                throw new ValidationException(chainedHeader.Hash);
            }

            // remove the transaction
            this.chainStateBuilderStorage.RemoveTransaction(tx.Hash);

            // remove the transaction outputs
            for (var outputIndex = 0U; outputIndex < tx.Outputs.Count; outputIndex++)
                this.chainStateBuilderStorage.RemoveOutput(new TxOutputKey(tx.Hash, outputIndex));
        }

        public void Unspend(TxInput input, ChainedHeader chainedHeader, Dictionary<UInt256, SpentTx> spentTransactions, Dictionary<TxOutputKey, TxOutput> spentOutputs)
        {
            //TODO currently a MissingDataException will get thrown if the rollback information is missing
            //TODO rollback is still possible if any resurrecting transactions can be found
            //TODO the network does not allow arbitrary transaction lookup, but if the transactions can be retrieved then this code should allow it

            //// retrieve rollback information
            //UInt256 prevTxBlockHash;
            //if (!spentTransactions.TryGetValue(input.PreviousTxOutputKey.TxHash, out prevTxBlockHash))
            //{
            //    //TODO throw should indicate rollback info is missing
            //    throw new MissingDataException(null);
            //}

            // retrieve previous output
            TxOutput prevTxOutput;
            if (!spentOutputs.TryGetValue(input.PreviousTxOutputKey, out prevTxOutput))
                throw new Exception("TODO - corruption");

            // retrieve transaction output states, if not found then a fully spent transaction is being resurrected
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            {
                // retrieve spent transaction
                SpentTx prevSpentTx;
                if (!spentTransactions.TryGetValue(input.PreviousTxOutputKey.TxHash, out prevSpentTx))
                    throw new Exception("TODO - corruption");

                // create fully spent transaction output state
                unspentTx = new UnspentTx(prevSpentTx.ConfirmedBlockHash, prevSpentTx.OutputCount, OutputState.Spent);
            }

            // retrieve previous output index
            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);
            if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
                throw new Exception("TODO - corruption");

            // check that output isn't already considered unspent
            if (unspentTx.OutputStates[outputIndex] == OutputState.Unspent)
                throw new ValidationException(chainedHeader.Hash);

            // mark output as unspent
            this.chainStateBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx.SetOutputState(outputIndex, OutputState.Unspent));

            // add transaction output back to utxo
            this.chainStateBuilderStorage.AddOutput(input.PreviousTxOutputKey, prevTxOutput);
        }

        private void SaveRollbackInformation(UInt256 blockHash, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
        {
            spentTransactionsCache[blockHash] = this.spentTransactions.ToImmutable();
            this.spentTransactions.Clear();

            spentOutputsCache[blockHash] = this.spentOutputs.ToImmutable();
            this.spentOutputs.Clear();
        }

        public void Flush()
        {
            this.chainStateBuilderStorage.Flush();
        }

        public Utxo ToImmutable(UInt256 blockHash)
        {
            return new Utxo(chainStateBuilderStorage.ToImmutable(blockHash));
        }

        private void BeginTransaction()
        {
            this.chainStateBuilderStorage.BeginTransaction();
            this.savedChain = this.chain.ToImmutable();
        }

        private void CommitTransaction()
        {
            this.chainStateBuilderStorage.CommitTransaction();
            this.savedChain = null;
        }

        private void RollbackTransaction()
        {
            this.chainStateBuilderStorage.RollbackTransaction();
            this.chain = this.savedChain.ToBuilder();
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
                if (blockchain.Blocks[0].Hash != genesisBlock.Hash)
                    throw new ValidationException(blockchain.Blocks[0].Hash);

                // get genesis block header
                var chainGenesisBlockHeader = this.blockHeaderCache[blockchain.Blocks[0].Hash];

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
                    var chainedHeader = blockchain.Blocks[height];

                    // verify height
                    if (chainedHeader.Height != height)
                        throw new ValidationException(chainedHeader.Hash);

                    // verify blockchain linking
                    if (chainedHeader.PreviousBlockHash != expectedPreviousBlockHash)
                        throw new ValidationException(chainedHeader.Hash);

                    // verify block exists
                    var blockHeader = this.blockHeaderCache[chainedHeader.Hash];

                    // verify block metadata matches header values
                    if (blockHeader.PreviousBlock != chainedHeader.PreviousBlockHash)
                        throw new ValidationException(chainedHeader.Hash);

                    // verify block header hash
                    if (DataCalculator.CalculateBlockHash(blockHeader) != chainedHeader.Hash)
                        throw new ValidationException(chainedHeader.Hash);

                    // next block metadata should have the current metadata's hash as its previous hash value
                    expectedPreviousBlockHash = chainedHeader.Hash;
                }

                // all validation passed
            }
            finally
            {
                stopwatch.Stop();
                this.logger.Info("Blockchain revalidation: {0:#,##0.000000}s".Format2(stopwatch.ElapsedSecondsFloat()));
            }
        }

        private IEnumerable<Tuple<int, ChainedBlock>> BlockLookAhead(IEnumerable<Tuple<int, ChainedHeader>> chain, int lookAhead)
        {
            return chain
                .Select(
                    chainedHeaderTuple =>
                    {
                        try
                        {
                            var direction = chainedHeaderTuple.Item1;
                            
                            var chainedHeader = chainedHeaderTuple.Item2;
                            var block = new MethodTimer(false).Time("GetBlock", () =>
                                this.blockCache[chainedHeader.Hash]);

                            var chainedBlock = new ChainedBlock(chainedHeader, block);

                            return Tuple.Create(direction, chainedBlock);
                        }
                        catch (MissingDataException e)
                        {
                            this.logger.Info("Stalled, MissingDataException: {0}".Format2(e.Key));
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