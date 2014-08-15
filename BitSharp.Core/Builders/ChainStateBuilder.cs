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
using BitSharp.Core.ExtensionMethods;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Workers;
using System.Security.Cryptography;
using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
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

        private readonly BlockValidator blockValidator;

        private bool inTransaction;
        private readonly DisposeHandle<IChainStateCursor> chainStateCursorHandle;
        private readonly IChainStateCursor chainStateCursor;
        private ChainBuilder chain;
        private readonly UtxoBuilder utxoBuilder;

        private readonly ReaderWriterLockSlim commitLock;

        private readonly BuilderStats stats;

        public ChainStateBuilder(Logger logger, IBlockchainRules rules, CoreStorage coreStorage)
        {
            this.logger = logger;
            this.sha256 = new SHA256Managed();
            this.rules = rules;
            this.coreStorage = coreStorage;

            this.blockValidator = new BlockValidator(this.coreStorage, this.rules, this.logger);

            this.chainStateCursorHandle = coreStorage.OpenChainStateCursor();
            this.chainStateCursor = this.chainStateCursorHandle.Item;

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
                this.blockValidator,
                this.chainStateCursorHandle,
                this.stats,
            }.DisposeList();
        }

        public Chain Chain { get { return this.chain.ToImmutable(); } }

        public BuilderStats Stats { get { return this.stats; } }

        public void AddBlock(ChainedHeader chainedHeader, IEnumerable<Transaction> transactions)
        {
            AddBlock(chainedHeader, transactions.Select((tx, txIndex) => new BlockTx(txIndex, depth: 0, hash: tx.Hash, pruned: false, transaction: tx)));
        }

        public void AddBlock(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes)
        {
            var savedChain = this.chain.ToImmutable();
            this.BeginTransaction();
            try
            {
                using (var session = this.blockValidator.StartValidation(chainedHeader))
                {
                    // add the block to the chain
                    this.chain.AddBlock(chainedHeader);
                    this.chainStateCursor.AddChainedHeader(chainedHeader);

                    // calculate the new block utxo, double spends will be checked for
                    new MethodTimer(false).Time("CalculateUtxo", () =>
                    {
                        // ignore transactions on geneis block
                        if (chainedHeader.Height > 0)
                        {
                            foreach (var pendingTx in this.utxoBuilder.CalculateUtxo(this.chain.ToImmutable(), blockTxes.Select(x => x.Transaction)))
                            {
                                this.blockValidator.AddPendingTx(pendingTx);

                                // track stats
                                this.stats.txCount++;
                                this.stats.inputCount += pendingTx.Transaction.Inputs.Length;
                                this.stats.txRateMeasure.Tick();
                                this.stats.inputRateMeasure.Tick(pendingTx.Transaction.Inputs.Length);
                            }
                        }

                        // finished queuing up block's txes
                        this.blockValidator.CompleteAdding();

                        // track stats
                        this.stats.blockCount++;
                    });

                    // wait for block validation to complete
                    this.blockValidator.WaitToComplete();

                    // check tx loader results
                    if (this.blockValidator.TxLoaderExceptions.Count > 0)
                    {
                        throw new AggregateException(this.blockValidator.TxLoaderExceptions);
                    }

                    // check tx validation results
                    if (this.blockValidator.TxValidatorExceptions.Count > 0)
                    {
                        throw new AggregateException(this.blockValidator.TxValidatorExceptions);
                    }

                    // check script validation results
                    if (this.blockValidator.ScriptValidatorExceptions.Count > 0)
                    {
                        if (!this.rules.IgnoreScriptErrors)
                            throw new AggregateException(this.blockValidator.ScriptValidatorExceptions);
                        else
                            this.logger.Info("Ignoring script errors in block: {0,9:#,##0}, errors: {1:#,##0}".Format2(chainedHeader.Height, this.blockValidator.ScriptValidatorExceptions.Count));
                    }
                }

                // commit the chain state
                this.CommitTransaction();
            }
            catch (Exception)
            {
                // rollback the chain state on error
                this.RollbackTransaction();
                this.chain = savedChain.ToBuilder();
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
                IImmutableList<SpentTx> spentTxes;
                if (!this.chainStateCursor.TryGetBlockSpentTxes(chainedHeader.Height, out spentTxes))
                    throw new ValidationException(chainedHeader.Height);

                var spentTxesDictionary =
                    ImmutableDictionary.CreateRange(
                        spentTxes.Select(spentTx => new KeyValuePair<UInt256, SpentTx>(spentTx.TxHash, spentTx)));

                // rollback the utxo
                this.utxoBuilder.RollbackUtxo(chainedHeader, blockTxes, spentTxesDictionary);

                //TODO this needs to happen in the same transaction
                // remove the rollback information
                this.chainStateCursor.TryRemoveBlockSpentTxes(chainedHeader.Height);

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
            get { return this.chainStateCursor.UnspentTxCount; }
        }

        private UInt256 GetOutputScripHash(TxOutput txOutput)
        {
            return new UInt256(this.sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()));
        }

        //TODO cache the latest immutable snapshot
        public ChainState ToImmutable()
        {
            return this.commitLock.DoRead(() =>
            {
                return new ChainState(this.chain.ToImmutable(), this.coreStorage.StorageManager);
            });
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
            private static readonly TimeSpan sampleCutoff = TimeSpan.FromMinutes(5);
            private static readonly TimeSpan sampleResolution = TimeSpan.FromSeconds(5);

            public Stopwatch durationStopwatch = Stopwatch.StartNew();
            public Stopwatch validateStopwatch = new Stopwatch();

            public long blockCount;
            public long txCount;
            public long inputCount;

            public readonly RateMeasure blockRateMeasure = new RateMeasure(sampleCutoff, sampleResolution);
            public readonly RateMeasure txRateMeasure = new RateMeasure(sampleCutoff, sampleResolution);
            public readonly RateMeasure inputRateMeasure = new RateMeasure(sampleCutoff, sampleResolution);

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