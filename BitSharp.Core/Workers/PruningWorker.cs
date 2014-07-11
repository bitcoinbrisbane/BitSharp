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
        private readonly ChainStateBuilder chainStateBuilder;
        private readonly IBlockchainRules rules;

        //TODO
        private int lastPruneHeight;

        public PruningWorker(WorkerConfig workerConfig, CoreStorage coreStorage, ChainStateBuilder chainStateBuilder, Logger logger, IBlockchainRules rules)
            : base("PruningWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.coreStorage = coreStorage;
            this.chainStateBuilder = chainStateBuilder;
            this.rules = rules;

            this.lastPruneHeight = 0;

            this.Mode = PruningMode.RollbackOnly;
        }

        public PruningMode Mode { get; set; }

        protected override void WorkAction()
        {
            var blocksPerDay = 144;
            var pruneBuffer = blocksPerDay * 7;

            var chain = this.chainStateBuilder.Chain;
            var minHeight = this.lastPruneHeight;
            var maxHeight = chain.Blocks.Count - pruneBuffer;

            if (maxHeight < minHeight)
                return;

            switch (this.Mode)
            {
                case PruningMode.RollbackOnly:
                    var stopwatch = Stopwatch.StartNew();
                    for (var i = minHeight; i <= maxHeight; i++)
                    {
                        // cooperative loop
                        this.ThrowIfCancelled();

                        var pruneData = new Dictionary<UInt256, List<int>>();

                        foreach (var tuple in this.chainStateBuilder.ReadSpentTransactions(i))
                        {
                            var addedBlockIndex = tuple.Item1;
                            var txIndex = tuple.Item2;
                            var addedBlockHash = chain.Blocks[addedBlockIndex].Hash;

                            if (!pruneData.ContainsKey(addedBlockHash))
                                pruneData[addedBlockHash] = new List<int>();

                            pruneData[addedBlockHash].Add(txIndex);
                        }

                        foreach (var keyPair in pruneData)
                        {
                            var addedBlockHash = keyPair.Key;
                            var txIndices = keyPair.Value;

                            this.coreStorage.PruneElements(addedBlockHash, txIndices);
                        }

                        //TODO properly sync commits before removing
                        this.chainStateBuilder.RemoveSpentTransactions(i);

                        //if (i % 1000 == 0)
                        //    this.logger.Info("Pruned to block: {0:#,##0}, took: {1:#,##0.000}s".Format2(i, stopwatch.Elapsed.TotalSeconds));
                    }

                    //var stopwatch = Stopwatch.StartNew();
                    //this.chainStateBuilder.RemoveSpentTransactionsToHeight(maxHeight);
                    //stopwatch.Stop();

                    this.lastPruneHeight = maxHeight;
                    this.logger.Info("Pruned to block: {0:#,##0}, took: {1:#,##0.000}s".Format2(maxHeight, stopwatch.Elapsed.TotalSeconds));

                    break;

                case PruningMode.RollbackAndBlocks:
                    break;
            }
        }
    }

    public enum PruningMode
    {
        RollbackOnly,
        RollbackAndBlocks
    }
}
