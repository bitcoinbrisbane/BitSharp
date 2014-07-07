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
        private static readonly int DUPE_COINBASE_1_HEIGHT = 91722;
        private static readonly UInt256 DUPE_COINBASE_1_HASH = UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber);
        private static readonly int DUPE_COINBASE_2_HEIGHT = 91812;
        private static readonly UInt256 DUPE_COINBASE_2_HASH = UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber);

        private readonly Logger logger;
        private readonly SHA256Managed sha256;
        private readonly IBlockchainRules rules;
        private readonly IBlockStorageNew blockCache;

        private readonly TxLoader txLoader;
        private readonly TxValidator txValidator;
        private readonly ScriptValidator scriptValidator;

        private bool inTransaction;
        private ChainBuilder chain;
        private Chain savedChain;
        private readonly IChainStateBuilderStorage chainStateBuilderStorage;

        //TODO when written more directly against Esent, these can be streamed out so an entire list doesn't need to be held in memory
        //private readonly ImmutableList<KeyValuePair<UInt256, SpentTx>>.Builder spentTransactions;
        //private readonly ImmutableList<KeyValuePair<TxOutputKey, TxOutput>>.Builder spentOutputs;

        private readonly ReaderWriterLockSlim commitLock;

        private readonly BuilderStats stats;

        public ChainStateBuilder(ChainBuilder chain, IChainStateBuilderStorage chainStateBuilderStorage, Logger logger, IBlockchainRules rules, IBlockStorageNew blockCache, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
        {
            this.logger = logger;
            this.sha256 = new SHA256Managed();
            this.rules = rules;
            this.blockCache = blockCache;
            //this.spentTransactionsCache = spentTransactionsCache;
            //this.spentOutputsCache = spentOutputsCache;

            var isConcurrent = true;
            this.scriptValidator = new ScriptValidator(this.logger, this.rules, isConcurrent);
            this.txValidator = new TxValidator(this.scriptValidator, this.logger, this.rules, isConcurrent);
            this.txLoader = new TxLoader(this.blockCache, this.txValidator, this.logger, this.rules, isConcurrent);

            this.chain = chain;
            this.chainStateBuilderStorage = chainStateBuilderStorage;

            //this.spentTransactions = ImmutableList.CreateBuilder<KeyValuePair<UInt256, SpentTx>>();
            //this.spentOutputs = ImmutableList.CreateBuilder<KeyValuePair<TxOutputKey, TxOutput>>();

            this.commitLock = new ReaderWriterLockSlim();

            this.stats = new BuilderStats();
        }

        ~ChainStateBuilder()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            this.txLoader.Dispose();
            this.txValidator.Dispose();
            this.scriptValidator.Dispose();

            this.stats.Dispose();
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

        public IDisposable Subscribe(IChainStateVisitor visitor)
        {
            throw new InvalidOperationException();
        }

        public void AddBlock(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes)
        {
            using (this.txLoader.Start())
            using (this.txValidator.Start())
            using (this.scriptValidator.Start(/*isConcurrent: chainedHeader.Height > 150.THOUSAND()*/))
            {
                this.BeginTransaction();
                try
                {
                    // add the block to the chain
                    this.Chain.AddBlock(chainedHeader);

                    // store block hash
                    this.chainStateBuilderStorage.BlockHeight = chainedHeader.Height;
                    this.chainStateBuilderStorage.BlockHash = chainedHeader.Hash;

                    // validate the block
                    //this.Stats.validateStopwatch.Start();
                    //new MethodTimer(false).Time("ValidateBlock", () =>
                    //    this.rules.ValidateBlock(chainedBlock, this));
                    //this.Stats.validateStopwatch.Stop();

                    // calculate the new block utxo, double spends will be checked for
                    new MethodTimer(false).Time("CalculateUtxo", () =>
                    {
                        foreach (var pendingTx in this.CalculateUtxo(chainedHeader, blockTxes))
                        {
                            this.txLoader.Add(pendingTx);
                        }

                        this.txLoader.CompleteAdding();
                    });

                    // collect rollback informatino and store it
                    //this.SaveRollbackInformation(chainedBlock.Hash, this.spentTransactionsCache, this.spentOutputsCache);

                    // wait for transactions to load
                    this.txLoader.WaitToComplete();
                    if (this.txLoader.Exceptions.Count > 0)
                    {
                        throw new AggregateException(this.txLoader.Exceptions);
                    }

                    // wait for transactions to validate
                    this.txValidator.WaitToComplete();
                    if (this.txValidator.Exceptions.Count > 0)
                    {
                        throw new AggregateException(this.txValidator.Exceptions);
                    }

                    // wait for scripts to validate
                    this.scriptValidator.WaitToComplete();
                    if (this.scriptValidator.Exceptions.Count > 0)
                    {
                        if (!MainnetRules.IgnoreScriptErrors)
                            throw new AggregateException(this.scriptValidator.Exceptions);
                        else
                            this.logger.Info("Ignoring script error in block: {0}".Format2(chainedHeader.Hash));
                    }

                    // commit the chain state
                    this.CommitTransaction();
                }
                catch (Exception)
                {
                    this.RollbackTransaction();
                    throw;
                }

                // MEASURE: Block Rate
                this.stats.blockRateMeasure.Tick();

                // blockchain processing statistics
                this.Stats.blockCount++;

                var logInterval = TimeSpan.FromSeconds(15);
                if (DateTime.UtcNow - this.Stats.lastLogTime >= logInterval)
                {
                    this.LogBlockchainProgress();
                    this.Stats.lastLogTime = DateTime.UtcNow;
                }
            }
        }

        public void RollbackBlock(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes)
        {
            //using (this.chainStateMonitor.Start())
            {
                this.BeginTransaction();
                try
                {
                    // remove the block from the chain
                    this.Chain.RemoveBlock(chainedHeader);

                    // store the block hash
                    this.chainStateBuilderStorage.BlockHeight = this.Chain.Height;
                    this.chainStateBuilderStorage.BlockHash = this.Chain.LastBlockHash;

                    // rollback the utxo
                    this.RollbackUtxo(chainedHeader, blockTxes);

                    // commit the chain state
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
            var blockRate = this.stats.blockRateMeasure.GetAverage(TimeSpan.FromSeconds(1));
            var txRate = this.stats.txRateMeasure.GetAverage(TimeSpan.FromSeconds(1));
            var inputRate = this.stats.inputRateMeasure.GetAverage(TimeSpan.FromSeconds(1));

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
                /*8*/ this.TransactionCount.ToString("#,##0")
                ));
        }

        public int TransactionCount
        {
            get { return this.chainStateBuilderStorage.TransactionCount; }
        }

        //public int OutputCount
        //{
        //    get { return this.chainStateBuilderStorage.OutputCount; }
        //}

        public bool TryGetUnspentTx(TxOutputKey txOutputKey, out UnspentTx unspentTx)
        {
            return this.chainStateBuilderStorage.TryGetTransaction(txOutputKey.TxHash, out unspentTx);
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            UnspentTx unspentTx;
            if (this.TryGetUnspentTx(txOutputKey, out unspentTx))
            {
                var blockIndex = this.chain.Blocks[unspentTx.BlockIndex].Hash;

                Transaction transaction;
                if (this.blockCache.TryGetTransaction(/*unspentTx.ConfirmedBlockHash,*/ blockIndex, unspentTx.TxIndex, out transaction))
                {
                    txOutput = transaction.Outputs[txOutputKey.TxOutputIndex.ToIntChecked()];
                    return true;
                }
                else
                {
                    txOutput = default(TxOutput);
                    return false;
                }
            }
            else
            {
                txOutput = default(TxOutput);
                return false;
            }
        }

        //public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> GetUnspentOutputs()
        //{
        //    return this.chainStateBuilderStorage.UnspentOutputs();
        //}

        private IEnumerable<PendingTx> CalculateUtxo(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes)
        {
            // don't include genesis block coinbase in utxo
            if (chainedHeader.Height <= 0)
                throw new InvalidOperationException();

            foreach (var blockTx in blockTxes)
            {
                var txIndex = blockTx.Index;
                this.stats.txCount++;

                //TODO apply real coinbase rule
                // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                if (txIndex == 0)
                {
                    var coinbaseTx = blockTx.Transaction;

                    // there exist two duplicate coinbases in the blockchain, which the design assumes to be impossible
                    // ignore the first occurrences of these duplicates so that they do not need to later be deleted from the utxo, an unsupported operation
                    // no other duplicates will occur again, it is now disallowed
                    if ((chainedHeader.Height == DUPE_COINBASE_1_HEIGHT && coinbaseTx.Hash == DUPE_COINBASE_1_HASH)
                        || (chainedHeader.Height == DUPE_COINBASE_2_HEIGHT && coinbaseTx.Hash == DUPE_COINBASE_2_HASH))
                    {
                        continue;
                    }

                    // mint the transaction's outputs in the utxo
                    yield return this.Mint(coinbaseTx, 0, chainedHeader, isCoinbase: true, spentTxes: ImmutableArray.Create<BlockTxKey>());
                }
                else
                {
                    var tx = blockTx.Transaction;
                    var spentTxes = ImmutableArray.CreateBuilder<BlockTxKey>();

                    // spend each of the transaction's inputs in the utxo
                    for (var inputIndex = 0; inputIndex < tx.Inputs.Length; inputIndex++)
                    {
                        this.stats.inputCount++;

                        var input = tx.Inputs[inputIndex];
                        spentTxes.Add(this.Spend(txIndex, tx, inputIndex, input, chainedHeader));

                        // MEASURE: Input Rate
                        this.stats.inputRateMeasure.Tick();
                    }

                    // mint the transaction's outputs in the utxo
                    yield return this.Mint(tx, txIndex, chainedHeader, false /*isCoinbase*/, spentTxes.ToImmutable());

                    // MEASURE: Transaction Rate
                    this.stats.txRateMeasure.Tick();
                }
            }
        }

        public PendingTx Mint(Transaction tx, int txIndex, ChainedHeader chainedHeader, bool isCoinbase, ImmutableArray<BlockTxKey> spentTxes)
        {
            // add transaction to the utxo
            var unspentTx = new UnspentTx(/*chainedHeader.Hash,*/ chainedHeader.Height, txIndex, tx.Outputs.Length, OutputState.Unspent);
            if (!this.chainStateBuilderStorage.TryAddTransaction(tx.Hash, unspentTx))
            {
                // duplicate transaction
                this.logger.Warn("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(chainedHeader.Height, chainedHeader.Hash.ToHexNumberString()));
                throw new ValidationException(chainedHeader.Hash);
            }

            return new PendingTx(txIndex, tx, chainedHeader, isCoinbase, spentTxes);
        }

        public BlockTxKey Spend(int txIndex, Transaction tx, int inputIndex, TxInput input, ChainedHeader chainedHeader)
        {
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            {
                // output wasn't present in utxo, invalid block
                throw new ValidationException(chainedHeader.Hash);
            }

            var unspentTxBlockHash = this.chain.Blocks[unspentTx.BlockIndex].Hash;
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

            //TODO don't remove data immediately, needs to stick around for rollback

            // update output states
            unspentTx = unspentTx.SetOutputState(outputIndex, OutputState.Spent);

            // update transaction output states in the utxo
            this.chainStateBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx);

            // remove fully spent transaction from the utxo
            if (unspentTx.OutputStates.All(x => x == OutputState.Spent))
            {
                this.chainStateBuilderStorage.RemoveTransaction(input.PreviousTxOutputKey.TxHash, chainedHeader.Height);

                // store rollback information, the block containing the previous transaction will need to be known during rollback
                //this.spentTransactions.Add(new KeyValuePair<UInt256, SpentTx>(input.PreviousTxOutputKey.TxHash, unspentTx.ToSpent()));

                if (false)
                {
                    this.blockCache.PruneElements(unspentTxBlockHash, new[] { unspentTx.TxIndex });
                }
            }

            // store rollback information, the output will need to be added back during rollback
            //this.spentOutputs.Add(new KeyValuePair<TxOutputKey, TxOutput>(input.PreviousTxOutputKey, prevOutput));

            // remove the output from the utxo
            //this.chainStateBuilderStorage.RemoveOutput(input.PreviousTxOutputKey);

            return new BlockTxKey(unspentTxBlockHash, unspentTx.TxIndex);
        }

        //TODO with the rollback information that's now being stored, rollback could be down without needing the block
        private void RollbackUtxo(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes)
        {
            //TODO don't reverse here, storage should be read in reverse
            foreach (var blockTx in blockTxes.Reverse())
            {
                var txIndex = blockTx.Index;

                if (txIndex == 0)
                {
                    var coinbaseTx = blockTx.Transaction;

                    // remove coinbase outputs
                    this.Unmint(coinbaseTx, chainedHeader, isCoinbase: true);
                }
                else
                {
                    var tx = blockTx.Transaction;

                    // remove outputs
                    this.Unmint(tx, chainedHeader, isCoinbase: false);

                    // remove inputs in reverse order
                    for (var inputIndex = tx.Inputs.Length - 1; inputIndex >= 0; inputIndex--)
                    {
                        var input = tx.Inputs[inputIndex];
                        this.Unspend(input, chainedHeader);
                    }
                }
            }
        }

        public void Unmint(Transaction tx, ChainedHeader chainedHeader, bool isCoinbase)
        {
            // ignore duplicate coinbases
            if ((chainedHeader.Height == DUPE_COINBASE_1_HEIGHT && tx.Hash == DUPE_COINBASE_1_HASH)
                || (chainedHeader.Height == DUPE_COINBASE_2_HEIGHT && tx.Hash == DUPE_COINBASE_2_HASH))
            {
                return;
            }

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
            this.chainStateBuilderStorage.RemoveTransaction(tx.Hash, /*TODO*/spentBlockIndex: -1);

            // remove the transaction outputs
            for (var outputIndex = 0; outputIndex < tx.Outputs.Length; outputIndex++)
            {
                var txOutput = tx.Outputs[outputIndex];
                var txOutputKey = new TxOutputKey(tx.Hash, (UInt32)outputIndex);

                //this.chainStateBuilderStorage.RemoveOutput(txOutputKey);

                // MONITOR: UnspendTxOutput
                //if (this.chainStateMonitor != null)
                //    this.chainStateMonitor.UnmintTxOutput(ChainPosition.Fake(), txOutputKey, txOutput, GetOutputScripHash(txOutput), isCoinbase);
            }
        }

        public void Unspend(TxInput input, ChainedHeader chainedHeader)
        {
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            {
                // output wasn't present in utxo, invalid block
                throw new ValidationException(chainedHeader.Hash);
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
        }

        private UInt256 GetOutputScripHash(TxOutput txOutput)
        {
            return new UInt256(this.sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()));
        }

        private void SaveRollbackInformation(UInt256 blockHash, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
        {
            //spentTransactionsCache[blockHash] = this.spentTransactions.ToImmutable();
            //this.spentTransactions.Clear();

            //spentOutputsCache[blockHash] = this.spentOutputs.ToImmutable();
            //this.spentOutputs.Clear();
        }

        public void Flush()
        {
            this.chainStateBuilderStorage.Flush();
        }

        public ChainState ToImmutable()
        {
            return this.commitLock.DoRead(() =>
                new ChainState(this.chain.ToImmutable(), new Utxo(chainStateBuilderStorage.ToImmutable())));
        }

        private void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            this.commitLock.EnterWriteLock();
            this.chainStateBuilderStorage.BeginTransaction();
            this.savedChain = this.chain.ToImmutable();
            this.inTransaction = true;
        }

        private void CommitTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateBuilderStorage.CommitTransaction();
            this.savedChain = null;
            this.inTransaction = false;
            this.commitLock.ExitWriteLock();
        }

        private void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateBuilderStorage.RollbackTransaction();
            this.chain = this.savedChain.ToBuilder();
            this.inTransaction = false;
            this.commitLock.ExitWriteLock();
        }

        public sealed class BuilderStats : IDisposable
        {
            public Stopwatch durationStopwatch = Stopwatch.StartNew();
            public Stopwatch validateStopwatch = new Stopwatch();

            public long blockCount;
            public long txCount;
            public long inputCount;

            public readonly RateMeasure blockRateMeasure = new RateMeasure();
            public readonly RateMeasure txRateMeasure = new RateMeasure();
            public readonly RateMeasure inputRateMeasure = new RateMeasure();

            public DateTime lastLogTime = DateTime.UtcNow;

            internal BuilderStats() { }

            public void Dispose()
            {
                this.blockRateMeasure.Dispose();
                this.txRateMeasure.Dispose();
                this.inputRateMeasure.Dispose();
            }
        }
    }
}