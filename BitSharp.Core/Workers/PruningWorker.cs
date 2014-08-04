using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.ExtensionMethods;
using BitSharp.Core.Builders;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace BitSharp.Core.Workers
{
    internal class PruningWorker : Worker
    {
        private readonly Logger logger;
        private readonly CoreStorage coreStorage;
        private readonly ChainStateWorker chainStateWorker;
        private readonly ChainStateBuilder chainStateBuilder;
        private readonly IBlockchainRules rules;

        private readonly IChainStateCursor chainStateCursor;

        //TODO
        private int lastPruneHeight;

        public PruningWorker(WorkerConfig workerConfig, CoreStorage coreStorage, ChainStateWorker chainStateWorker, ChainStateBuilder chainStateBuilder, Logger logger, IBlockchainRules rules)
            : base("PruningWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.coreStorage = coreStorage;
            this.chainStateWorker = chainStateWorker;
            this.chainStateBuilder = chainStateBuilder;
            this.rules = rules;

            this.chainStateCursor = coreStorage.OpenChainStateCursor();

            this.lastPruneHeight = 0;

            this.Mode = PruningMode.RollbackAndBlocks;
        }

        protected override void SubDispose()
        {
            this.chainStateCursor.Dispose();
        }

        public PruningMode Mode { get; set; }

        protected override void WorkAction()
        {
            var blocksPerDay = 144;
            var pruneBuffer = blocksPerDay * 7;

            var chain = this.chainStateBuilder.Chain;
            var minHeight = this.lastPruneHeight + 1;
            var maxHeight = chain.Height - pruneBuffer;

            if (maxHeight <= minHeight)
                return;

            //if (maxHeight - this.lastPruneHeight > blocksPerDay)
            //    this.chainStateWorker.Stop();

            this.logger.Info(@"Begin pruning from block {0:#,##0} to {1:#,##0}".Format2(minHeight, maxHeight));

            switch (this.Mode)
            {
                case PruningMode.RollbackOnly:
                    {
                        var committed = false;
                        this.chainStateCursor.BeginTransaction();
                        try
                        {
                            this.chainStateCursor.RemoveSpentTransactionsToHeight(maxHeight);

                            this.chainStateCursor.CommitTransaction();
                            committed = true;
                        }
                        finally
                        {
                            if (!committed)
                                this.chainStateCursor.RollbackTransaction();
                        }
                    }
                    break;

                case PruningMode.RollbackAndBlocks:
                    {
                        var committed = false;
                        this.chainStateCursor.BeginTransaction();
                        try
                        {
                            var totalStopwatch = Stopwatch.StartNew();
                            var gatherStopwatch = new Stopwatch();
                            var pruneStopwatch = new Stopwatch();
                            var flushStopwatch = new Stopwatch();
                            var cleanStopwatch = new Stopwatch();
                            var commitStopwatch = new Stopwatch();

                            var txCount = 0;

                            var pruneData = new SortedDictionary<int, List<int>>();

                            for (var blockHeight = minHeight; blockHeight <= maxHeight; blockHeight++)
                            {
                                // cooperative loop
                                this.ThrowIfCancelled();

                                // collect pruning information and group it by block
                                gatherStopwatch.Start();
                                IImmutableList<SpentTx> spentTxes;
                                if (this.chainStateCursor.TryGetBlockSpentTxes(blockHeight, out spentTxes))
                                {
                                    foreach (var spentTx in spentTxes)
                                    {
                                        // cooperative loop
                                        this.ThrowIfCancelled();

                                        if (!pruneData.ContainsKey(spentTx.ConfirmedBlockIndex))
                                            pruneData[spentTx.ConfirmedBlockIndex] = new List<int>();

                                        txCount++;
                                        pruneData[spentTx.ConfirmedBlockIndex].Add(spentTx.TxIndex);
                                    }
                                }
                                gatherStopwatch.Stop();
                            }

                            // prune the spent transactions from each block
                            pruneStopwatch.Start();
                            //foreach (var keyPair in pruneData)
                            Parallel.ForEach(pruneData,
                                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                                keyPair =>
                                {
                                    // cooperative loop
                                    this.ThrowIfCancelled();

                                    var confirmedBlockIndex = keyPair.Key;
                                    var confirmedBlockHash = chain.Blocks[confirmedBlockIndex].Hash;
                                    var spentTxIndices = keyPair.Value;
                                    spentTxIndices.Sort();

                                    this.coreStorage.PruneElements(confirmedBlockHash, spentTxIndices);
                                });
                            pruneStopwatch.Stop();

                            flushStopwatch.Start();
                            this.coreStorage.FlushBlockTxes();
                            flushStopwatch.Stop();

                            //TODO properly sync commits before removing
                            // remove the pruning information
                            cleanStopwatch.Start();
                            for (var blockHeight = minHeight; blockHeight <= maxHeight; blockHeight++)
                            {
                                this.chainStateCursor.TryRemoveBlockSpentTxes(blockHeight);
                            }
                            cleanStopwatch.Stop();
                            //}

                            commitStopwatch.Start();
                            this.chainStateCursor.CommitTransaction();
                            committed = true;
                            commitStopwatch.Stop();

                            this.lastPruneHeight = maxHeight;

                            var txRate = txCount / totalStopwatch.Elapsed.TotalSeconds;
                            this.logger.Info(
@"Pruned from block {0:#,##0} to {1:#,##0}:
- tx count: {2,10:#,##0}
- tx rate:  {3,10:#,##0}/s
- gather:       {4,10:#,##0.000}s
- prune:        {5,10:#,##0.000}s
- flush:        {6,10:#,##0.000}s
- clean:        {7,10:#,##0.000}s
- commit:       {8,10:#,##0.000}s
- TOTAL:        {9,10:#,##0.000}s"
                                .Format2(minHeight, maxHeight, txCount, txRate, gatherStopwatch.Elapsed.TotalSeconds, pruneStopwatch.Elapsed.TotalSeconds, flushStopwatch.Elapsed.TotalSeconds, cleanStopwatch.Elapsed.TotalSeconds, commitStopwatch.Elapsed.TotalSeconds, totalStopwatch.Elapsed.TotalSeconds));
                        }
                        finally
                        {
                            if (!committed)
                                this.chainStateCursor.RollbackTransaction();
                        }
                    }
                    break;
            }

            this.chainStateWorker.Start();
            this.chainStateWorker.NotifyWork();
        }
    }

    public enum PruningMode
    {
        RollbackOnly,
        RollbackAndBlocks
    }
}
