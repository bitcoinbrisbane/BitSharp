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
    internal class ChainStateBuilder : IDisposable
    {
        private readonly Logger logger;
        private readonly SHA256Managed sha256;
        private readonly IBlockchainRules rules;
        private readonly CoreStorage coreStorage;

        private readonly TxPrevOutputLoader txPrevOutputLoader;
        private readonly TxValidator txValidator;
        private readonly ScriptValidator scriptValidator;

        private bool inTransaction;
        private readonly IChainStateCursor chainStateCursor;
        private ChainBuilder chain;
        private readonly UtxoBuilder utxoBuilder;

        private readonly ReaderWriterLockSlim commitLock;

        private readonly BuilderStats stats;

        public ChainStateBuilder(IChainStateCursor chainStateCursor, Logger logger, IBlockchainRules rules, CoreStorage coreStorage)
        {
            this.logger = logger;
            this.sha256 = new SHA256Managed();
            this.rules = rules;
            this.coreStorage = coreStorage;

            var isConcurrent = true;
            this.scriptValidator = new ScriptValidator(this.logger, this.rules, isConcurrent);
            this.txValidator = new TxValidator(this.scriptValidator, this.logger, this.rules, isConcurrent);
            this.txPrevOutputLoader = new TxPrevOutputLoader(this.coreStorage, this.txValidator, this.logger, this.rules, isConcurrent);

            this.chainStateCursor = chainStateCursor;

            this.chain = new ChainBuilder(chainStateCursor.ReadChain());
            this.utxoBuilder = new UtxoBuilder(chainStateCursor, logger);

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

            new IDisposable[]
            {
                this.txPrevOutputLoader,
                this.txValidator,
                this.scriptValidator,
                this.stats,
            }.DisposeList();
        }

        public Chain Chain { get { return this.chain.ToImmutable(); } }

        public BuilderStats Stats { get { return this.stats; } }

        public void AddBlock(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes)
        {
            using (this.txPrevOutputLoader.Start())
            using (this.txValidator.Start())
            using (this.scriptValidator.Start(/*isConcurrent: chainedHeader.Height > 150.THOUSAND()*/))
            {
                var savedChain = this.chain.ToImmutable();
                this.BeginTransaction();
                try
                {
                    // add the block to the chain
                    this.chain.AddBlock(chainedHeader);
                    this.chainStateCursor.AddChainedHeader(chainedHeader);

                    // validate the block
                    //this.Stats.validateStopwatch.Start();
                    //new MethodTimer(false).Time("ValidateBlock", () =>
                    //    this.rules.ValidateBlock(chainedBlock, this));
                    //this.Stats.validateStopwatch.Stop();

                    // calculate the new block utxo, double spends will be checked for
                    new MethodTimer(false).Time("CalculateUtxo", () =>
                    {
                        foreach (var pendingTx in this.utxoBuilder.CalculateUtxo(this.chain.ToImmutable(), blockTxes.Select(x => x.Transaction)))
                        {
                            this.txPrevOutputLoader.Add(pendingTx);

                            // track stats
                            this.stats.txCount++;
                            this.stats.inputCount += pendingTx.transaction.Inputs.Length;
                            this.stats.txRateMeasure.Tick();
                            this.stats.inputRateMeasure.Tick(pendingTx.transaction.Inputs.Length);
                        }

                        this.txPrevOutputLoader.CompleteAdding();

                        // track stats
                        this.stats.blockCount++;
                    });

                    // wait for transactions to load
                    this.txPrevOutputLoader.WaitToComplete();
                    if (this.txPrevOutputLoader.Exceptions.Count > 0)
                    {
                        throw new AggregateException(this.txPrevOutputLoader.Exceptions);
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
                            this.logger.Info("Ignoring script errors in block: {0,9:#,##0}, errors: {1:#,##0}".Format2(chainedHeader.Height, this.scriptValidator.Exceptions.Count));
                    }

                    // commit the chain state
                    this.CommitTransaction();
                }
                catch (Exception)
                {
                    this.chain = savedChain.ToBuilder();
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
            var savedChain = this.chain.ToImmutable();
            this.BeginTransaction();
            try
            {
                // remove the block from the chain
                this.chain.RemoveBlock(chainedHeader);
                this.chainStateCursor.RemoveChainedHeader(chainedHeader);

                // read spent transaction rollback information
                var spentTxes =
                    ImmutableDictionary.CreateRange(
                        this.chainStateCursor.ReadSpentTransactions(chainedHeader.Height)
                            .Select(spentTx => new KeyValuePair<UInt256, SpentTx>(spentTx.TxHash, spentTx)));

                // rollback the utxo
                this.utxoBuilder.RollbackUtxo(chainedHeader, blockTxes, spentTxes);

                //TODO this needs to happen in the same transaction
                // remove the rollback information
                this.chainStateCursor.RemoveSpentTransactions(chainedHeader.Height);

                // commit the chain state
                this.CommitTransaction();
            }
            catch (Exception)
            {
                this.chain = savedChain.ToBuilder();
                this.RollbackTransaction();
                throw;
            }
        }

        public void LogBlockchainProgress()
        {
            var elapsedSeconds = this.Stats.durationStopwatch.Elapsed.TotalSeconds;
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
                /*0*/ this.Chain.Height.ToString("#,##0"),
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
            get { return this.chainStateCursor.TransactionCount; }
        }

        public IEnumerable<SpentTx> ReadSpentTransactions(int spentBlockIndex)
        {
            return this.chainStateCursor.ReadSpentTransactions(spentBlockIndex);
        }

        public void RemoveSpentTransactions(int spentBlockIndex)
        {
            this.chainStateCursor.RemoveSpentTransactions(spentBlockIndex);
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            this.chainStateCursor.RemoveSpentTransactionsToHeight(spentBlockIndex);
        }

        private UInt256 GetOutputScripHash(TxOutput txOutput)
        {
            return new UInt256(this.sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()));
        }

        public ChainState ToImmutable()
        {
            return this.commitLock.DoRead(() =>
                new ChainState(this.chain.ToImmutable(), this.chainStateCursor));
        }

        private void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            this.commitLock.EnterWriteLock();
            this.chainStateCursor.BeginTransaction();
            this.inTransaction = true;
        }

        private void CommitTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateCursor.CommitTransaction();
            this.inTransaction = false;
            this.commitLock.ExitWriteLock();
        }

        private void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateCursor.RollbackTransaction();
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