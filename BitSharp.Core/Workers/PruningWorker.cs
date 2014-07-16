using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
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

            this.Mode = PruningMode.RollbackAndBlocks;
        }

        public PruningMode Mode { get; set; }

        protected override void WorkAction()
        {
            var blocksPerDay = 144;
            var pruneBuffer = blocksPerDay * 7;

            var chain = this.chainStateBuilder.Chain;
            var minHeight = this.lastPruneHeight + 1;
            var maxHeight = chain.Blocks.Count - pruneBuffer;

            if (maxHeight <= minHeight)
                return;

            if (maxHeight - this.lastPruneHeight > blocksPerDay)
                this.chainStateWorker.Stop();

            this.logger.Info(@"Begin pruning from block {0:#,##0} to {1:#,##0}".Format2(minHeight, maxHeight));

            switch (this.Mode)
            {
                case PruningMode.RollbackOnly:
                    this.chainStateBuilder.RemoveSpentTransactionsToHeight(maxHeight);
                    break;

                case PruningMode.RollbackAndBlocks:
                    var totalStopwatch = Stopwatch.StartNew();
                    var gatherStopwatch = new Stopwatch();
                    var pruneStopwatch = new Stopwatch();
                    var cleanStopwatch = new Stopwatch();

                    var txCount = 0;

                    for (var blockHeight = minHeight; blockHeight <= maxHeight; blockHeight++)
                    {
                        // cooperative loop
                        this.ThrowIfCancelled();

                        // collect pruning information and group it by block
                        gatherStopwatch.Start();
                        var pruneData = new SortedDictionary<int, List<int>>();
                        foreach (var spentTx in this.chainStateBuilder.ReadSpentTransactions(blockHeight))
                        {
                            // cooperative loop
                            this.ThrowIfCancelled();

                            if (!pruneData.ContainsKey(spentTx.ConfirmedBlockIndex))
                                pruneData[spentTx.ConfirmedBlockIndex] = new List<int>();

                            txCount++;
                            pruneData[spentTx.ConfirmedBlockIndex].Add(spentTx.TxIndex);
                        }
                        gatherStopwatch.Stop();

                        // prune the spent transactions from each block
                        pruneStopwatch.Start();
                        Parallel.ForEach(pruneData, keyPair =>
                        //foreach (var keyPair in pruneData)
                        {
                            // cooperative loop
                            this.ThrowIfCancelled();

                            var confirmedBlockIndex = keyPair.Key;
                            var confirmedBlockHash = chain.Blocks[confirmedBlockIndex].Hash;
                            var spentTxIndices = keyPair.Value;

                            this.coreStorage.PruneElements(confirmedBlockHash, spentTxIndices);
                        });
                        pruneStopwatch.Stop();

                        //TODO properly sync commits before removing
                        // remove the pruning information
                        cleanStopwatch.Start();
                        this.chainStateBuilder.RemoveSpentTransactions(blockHeight);
                        cleanStopwatch.Stop();
                    }

                    this.lastPruneHeight = maxHeight;

                    var txRate = txCount / totalStopwatch.Elapsed.TotalSeconds;
                    this.logger.Info(
@"Pruned from block {0:#,##0} to {1:#,##0}:
    - tx count: {2,10:#,##0}
    - tx rate:  {3,10:#,##0}/s
    - gather:       {4,10:#,##0.000}s
    - prune:        {5,10:#,##0.000}s
    - clean:        {6,10:#,##0.000}s
    - TOTAL:        {7,10:#,##0.000}s"
                        .Format2(minHeight, maxHeight, txCount, txRate, gatherStopwatch.Elapsed.TotalSeconds, pruneStopwatch.Elapsed.TotalSeconds, cleanStopwatch.Elapsed.TotalSeconds, totalStopwatch.Elapsed.TotalSeconds));

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
