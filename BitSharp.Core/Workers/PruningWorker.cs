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
        private readonly ChainStateBuilder chainStateBuilder;
        private readonly IBlockchainRules rules;

        public PruningWorker(WorkerConfig workerConfig, ChainStateBuilder chainStateBuilder, Logger logger, IBlockchainRules rules)
            : base("PruningWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.chainStateBuilder = chainStateBuilder;
            this.rules = rules;

            this.Mode = PruningMode.RollbackOnly;
        }

        public PruningMode Mode { get; set; }

        protected override void WorkAction()
        {
            var blocksPerDay = 144;
            var pruneBuffer = blocksPerDay * 7;

            var chain = this.chainStateBuilder.Chain;
            var minHeight = 0;
            var maxHeight = chain.Blocks.Count - pruneBuffer;

            switch (this.Mode)
            {
                case PruningMode.RollbackOnly:
                    //for (var i = minHeight; i <= maxHeight; i++)
                    //{
                    //    // cooperative loop
                    //    this.ThrowIfCancelled();

                    //    this.chainStateBuilder.RemoveSpentTransactions(i);

                    //    if (i % 1000 == 0)
                    //        this.logger.Info("Pruned to block: {0:#,##0}".Format2(i));
                    //}

                    var stopwatch = Stopwatch.StartNew();
                    this.chainStateBuilder.RemoveSpentTransactionsToHeight(maxHeight);
                    stopwatch.Stop();

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
