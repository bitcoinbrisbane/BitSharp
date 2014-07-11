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
        private static readonly int DUPE_COINBASE_1_HEIGHT = 91722;
        private static readonly UInt256 DUPE_COINBASE_1_HASH = UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber);
        private static readonly int DUPE_COINBASE_2_HEIGHT = 91812;
        private static readonly UInt256 DUPE_COINBASE_2_HASH = UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber);

        private readonly Logger logger;
        private readonly SHA256Managed sha256;
        private readonly IBlockchainRules rules;
        private readonly CoreStorage coreStorage;

        private readonly TxPrevOutputLoader txPrevOutputLoader;
        private readonly TxValidator txValidator;
        private readonly ScriptValidator scriptValidator;

        private bool inTransaction;
        private readonly IChainStateBuilderStorage chainStateBuilderStorage;
        private readonly UtxoBuilder utxoBuilder;

        private readonly ReaderWriterLockSlim commitLock;

        private readonly BuilderStats stats;

        public ChainStateBuilder(IChainStateBuilderStorage chainStateBuilderStorage, Logger logger, IBlockchainRules rules, CoreStorage coreStorage)
        {
            this.logger = logger;
            this.sha256 = new SHA256Managed();
            this.rules = rules;
            this.coreStorage = coreStorage;

            var isConcurrent = true;
            this.scriptValidator = new ScriptValidator(this.logger, this.rules, isConcurrent);
            this.txValidator = new TxValidator(this.scriptValidator, this.logger, this.rules, isConcurrent);
            this.txPrevOutputLoader = new TxPrevOutputLoader(this.coreStorage, this.txValidator, this.logger, this.rules, isConcurrent);

            this.chainStateBuilderStorage = chainStateBuilderStorage;
            this.utxoBuilder = new UtxoBuilder(chainStateBuilderStorage, logger);

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

        public Chain Chain { get { return this.chainStateBuilderStorage.Chain; } }

        public BuilderStats Stats { get { return this.stats; } }

        public void AddBlock(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes)
        {
            using (this.txPrevOutputLoader.Start())
            using (this.txValidator.Start())
            using (this.scriptValidator.Start(/*isConcurrent: chainedHeader.Height > 150.THOUSAND()*/))
            {
                this.BeginTransaction();
                try
                {
                    // add the block to the chain
                    this.chainStateBuilderStorage.AddChainedHeader(chainedHeader);

                    // validate the block
                    //this.Stats.validateStopwatch.Start();
                    //new MethodTimer(false).Time("ValidateBlock", () =>
                    //    this.rules.ValidateBlock(chainedBlock, this));
                    //this.Stats.validateStopwatch.Stop();

                    // calculate the new block utxo, double spends will be checked for
                    new MethodTimer(false).Time("CalculateUtxo", () =>
                    {
                        foreach (var pendingTx in this.utxoBuilder.CalculateUtxo(chainedHeader, blockTxes.Select(x => x.Transaction)))
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
                        this.stats.blockRateMeasure.Tick();
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
            this.BeginTransaction();
            try
            {
                // remove the block from the chain
                this.chainStateBuilderStorage.RemoveChainedHeader(chainedHeader);

                // rollback the utxo
                this.utxoBuilder.RollbackUtxo(chainedHeader, blockTxes);

                // commit the chain state
                this.CommitTransaction();
            }
            catch (Exception)
            {
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
            get { return this.chainStateBuilderStorage.TransactionCount; }
        }

        public IEnumerable<Tuple<int, int>> ReadSpentTransactions(int spentBlockIndex)
        {
            return this.chainStateBuilderStorage.ReadSpentTransactions(spentBlockIndex);
        }

        public void RemoveSpentTransactions(int spentBlockIndex)
        {
            this.chainStateBuilderStorage.RemoveSpentTransactions(spentBlockIndex);
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            this.chainStateBuilderStorage.RemoveSpentTransactionsToHeight(spentBlockIndex);
        }

        private UInt256 GetOutputScripHash(TxOutput txOutput)
        {
            return new UInt256(this.sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()));
        }

        public ChainState ToImmutable()
        {
            return this.commitLock.DoRead(() =>
                new ChainState(this.chainStateBuilderStorage));
        }

        private void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            this.commitLock.EnterWriteLock();
            this.chainStateBuilderStorage.BeginTransaction();
            this.inTransaction = true;
        }

        private void CommitTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateBuilderStorage.CommitTransaction();
            this.inTransaction = false;
            this.commitLock.ExitWriteLock();
        }

        private void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateBuilderStorage.RollbackTransaction();
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