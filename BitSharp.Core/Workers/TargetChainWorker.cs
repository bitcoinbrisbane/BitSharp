using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Builders;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using Ninject;
using Ninject.Parameters;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Workers
{
    internal class TargetChainWorker : Worker
    {
        public event Action OnTargetBlockChanged;
        public event Action OnTargetChainChanged;

        private readonly Logger logger;
        private readonly IBlockchainRules rules;
        private readonly CoreStorage coreStorage;

        private readonly TargetBlockWorker targetBlockWorker;
        private Chain targetChain;

        public TargetChainWorker(WorkerConfig workerConfig, Logger logger, IBlockchainRules rules, CoreStorage coreStorage)
            : base("TargetChainWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.rules = rules;
            this.coreStorage = coreStorage;

            this.targetBlockWorker = new TargetBlockWorker(
                new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromMilliseconds(50), maxIdleTime: TimeSpan.MaxValue),
                this.logger, this.coreStorage);

            this.targetBlockWorker.TargetBlockChanged += HandleTargetBlockChanged;
            this.coreStorage.ChainedHeaderAdded += HandleChainedHeaderAdded;
            this.coreStorage.BlockInvalidated += HandleBlockInvalidated;
        }

        protected override void SubDispose()
        {
            // cleanup events
            this.targetBlockWorker.TargetBlockChanged -= HandleTargetBlockChanged;
            this.coreStorage.ChainedHeaderAdded -= HandleChainedHeaderAdded;
            this.coreStorage.BlockInvalidated -= HandleBlockInvalidated;

            // cleanup workers
            this.targetBlockWorker.Dispose();
        }

        public Chain TargetChain { get { return this.targetChain; } }

        public ChainedHeader TargetBlock { get { return this.targetBlockWorker.TargetBlock; } }

        internal TargetBlockWorker TargetBlockWorker { get { return this.targetBlockWorker; } }

        protected override void SubStart()
        {
            this.targetBlockWorker.Start();
            
            // acquire initial target block on startup
            this.targetBlockWorker.ForceWorkAndWait();
        }

        protected override void SubStop()
        {
            this.targetBlockWorker.Stop();
        }

        protected override void SubForceWork()
        {
            this.targetBlockWorker.ForceWorkAndWait();
        }

        protected override void WorkAction()
        {
            try
            {
                var targetBlockLocal = this.targetBlockWorker.TargetBlock;
                var targetChainLocal = this.targetChain;

                if (targetBlockLocal != null &&
                    (targetChainLocal == null || targetBlockLocal.Hash != targetChainLocal.LastBlock.Hash))
                {
                    var newTargetChain =
                        targetChainLocal != null
                            ? targetChainLocal.ToBuilder()
                            : new ChainBuilder(Chain.CreateForGenesisBlock(this.rules.GenesisChainedHeader));

                    var deltaBlockPath = new MethodTimer(false).Time("deltaBlockPath", () =>
                        new BlockchainWalker().GetBlockchainPath(newTargetChain.LastBlock, targetBlockLocal, blockHash => this.coreStorage.GetChainedHeader(blockHash)));

                    foreach (var rewindBlock in deltaBlockPath.RewindBlocks)
                    {
                        newTargetChain.RemoveBlock(rewindBlock);
                    }

                    foreach (var advanceBlock in deltaBlockPath.AdvanceBlocks)
                    {
                        newTargetChain.AddBlock(advanceBlock);
                    }

                    this.logger.Debug("Winning chained block {0} at height {1}, total work: {2}".Format2(newTargetChain.LastBlock.Hash.ToHexNumberString(), newTargetChain.Height, newTargetChain.LastBlock.TotalWork.ToString("X")));
                    this.targetChain = newTargetChain.ToImmutable();

                    var handler = this.OnTargetChainChanged;
                    if (handler != null)
                        handler();
                }
            }
            catch (MissingDataException) { }
        }

        private void HandleTargetBlockChanged()
        {
            this.NotifyWork();

            var handler = this.OnTargetBlockChanged;
            if (handler != null)
                handler();
        }

        private void HandleChainedHeaderAdded(ChainedHeader chainedHeader)
        {
            this.NotifyWork();
        }

        private void HandleBlockInvalidated(UInt256 blockHash)
        {
            this.NotifyWork();
        }
    }
}
