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

            this.lastPruneHeight = 0;
            this.Mode = PruningMode.None;
        }

        public PruningMode Mode { get; set; }

        public int PrunableHeight { get; set; }

        protected override void WorkAction()
        {
            if (this.Mode == PruningMode.None)
                return;

            var blocksPerDay = 144;
            var pruneBuffer = blocksPerDay * 7;

            var chain = this.chainStateBuilder.Chain;
            var minHeight = this.lastPruneHeight + 1;
            var maxHeight = Math.Min(chain.Height - pruneBuffer, this.PrunableHeight);

            if (maxHeight <= minHeight)
                return;

            //if (maxHeight - this.lastPruneHeight > blocksPerDay)
            //    this.chainStateWorker.Stop();

            this.logger.Info(@"Begin pruning from block {0:#,##0} to {1:#,##0}".Format2(minHeight, maxHeight));

            switch (this.Mode)
            {
                // remove just the information required to rollback blocks and to replay them
                case PruningMode.RollbackOnly:
                    using (var handle = coreStorage.OpenChainStateCursor())
                    {
                        var chainStateCursor = handle.Item;

                        // stats
                        var totalStopwatch = Stopwatch.StartNew();
                        var txCount = 0;

                        for (var blockHeight = minHeight; blockHeight <= maxHeight; blockHeight++)
                        {
                            // cooperative loop
                            this.ThrowIfCancelled();

                            chainStateCursor.BeginTransaction();

                            // retrieve the spent txes for this block
                            IImmutableList<SpentTx> spentTxes;
                            if (chainStateCursor.TryGetBlockSpentTxes(blockHeight, out spentTxes))
                            {
                                // remove each spent tx
                                foreach (var spentTx in spentTxes)
                                {
                                    // cooperative loop
                                    this.ThrowIfCancelled();

                                    txCount++;

                                    var wasRemoved = chainStateCursor.TryRemoveUnspentTx(spentTx.TxHash);
                                    //if (!wasRemoved)
                                    //    throw new Exception("TODO");
                                }

                                // remove spent txes for this block
                                var wasRemoved2 = chainStateCursor.TryRemoveBlockSpentTxes(blockHeight);
                                //if (!wasRemoved2)
                                //    throw new Exception("TODO");
                            }

                            chainStateCursor.CommitTransaction();
                            this.lastPruneHeight = maxHeight;
                        }

                        var txRate = txCount / totalStopwatch.Elapsed.TotalSeconds;
                        this.logger.Info(
@"Pruned from block {0:#,##0} to {1:#,##0}:
- tx count: {2,10:#,##0}
- tx rate:  {3,10:#,##0}/s
- TOTAL:        {4,10:#,##0.000}s"
                            .Format2(minHeight, maxHeight, txCount, txRate, totalStopwatch.Elapsed.TotalSeconds));
                    }
                    break;

                // remove spent transactions from block storage, in addition to the information required to rollback blocks and to replay them
                case PruningMode.RollbackAndBlocks:
                    using (var handle = coreStorage.OpenChainStateCursor())
                    {
                        var chainStateCursor = handle.Item;

                        // stats
                        var totalStopwatch = Stopwatch.StartNew();
                        var gatherStopwatch = new Stopwatch();
                        var pruneStopwatch = new Stopwatch();
                        var flushStopwatch = new Stopwatch();
                        var cleanStopwatch = new Stopwatch();
                        var commitStopwatch = new Stopwatch();
                        var txCount = 0;

                        // ensure chain state is flushed before pruning
                        flushStopwatch.Start();
                        chainStateCursor.Flush();
                        flushStopwatch.Stop();

                        // begin pruning transaction
                        chainStateCursor.BeginTransaction();

                        var pruneData = new SortedDictionary<int, List<int>>();

                        for (var blockHeight = minHeight; blockHeight <= maxHeight; blockHeight++)
                        {
                            // cooperative loop
                            this.ThrowIfCancelled();

                            // collect pruning information and group it by block
                            gatherStopwatch.Start();
                            IImmutableList<SpentTx> spentTxes;
                            if (chainStateCursor.TryGetBlockSpentTxes(blockHeight, out spentTxes))
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
                        var cancelled = false;
                        Parallel.ForEach(pruneData,
                            new ParallelOptions { MaxDegreeOfParallelism = 4 },
                            (keyPair, loopState) =>
                            {
                                // cooperative loop
                                if (!this.IsStarted)
                                {
                                    cancelled = true;
                                    loopState.Stop();
                                    return;
                                }

                                var confirmedBlockIndex = keyPair.Key;
                                var confirmedBlockHash = chain.Blocks[confirmedBlockIndex].Hash;
                                var spentTxIndices = keyPair.Value;
                                spentTxIndices.Sort();

                                this.coreStorage.PruneElements(confirmedBlockHash, spentTxIndices);
                            });
                        if (cancelled)
                            throw new OperationCanceledException();
                        pruneStopwatch.Stop();

                        flushStopwatch.Start();
                        this.coreStorage.FlushBlockTxes();
                        flushStopwatch.Stop();

                        //TODO properly sync commits before removing
                        // remove the pruning information
                        cleanStopwatch.Start();
                        for (var blockHeight = minHeight; blockHeight <= maxHeight; blockHeight++)
                        {
                            chainStateCursor.TryRemoveBlockSpentTxes(blockHeight);
                        }
                        cleanStopwatch.Stop();
                        //}

                        commitStopwatch.Start();
                        chainStateCursor.CommitTransaction();
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
                    break;
            }

            //this.chainStateWorker.Start();
            //this.chainStateWorker.NotifyWork();
        }
    }
}
