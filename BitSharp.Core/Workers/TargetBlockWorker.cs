using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Workers
{
    internal class TargetBlockWorker : Worker
    {
        public event Action TargetBlockChanged;

        private readonly CoreStorage coreStorage;

        private ChainedHeader targetBlock;
        private readonly ReaderWriterLockSlim targetBlockLock;

        public TargetBlockWorker(WorkerConfig workerConfig, Logger logger, CoreStorage coreStorage)
            : base("TargetBlockWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.coreStorage = coreStorage;

            this.targetBlock = null;
            this.targetBlockLock = new ReaderWriterLockSlim();

            // wire up cache events
            this.coreStorage.ChainedHeaderAdded += HandleChainedHeaderAdded;
            this.coreStorage.BlockInvalidated += HandleBlockInvalidated;
        }

        public ChainedHeader TargetBlock { get { return this.targetBlock; } }

        protected override void SubDispose()
        {
            // cleanup events
            this.coreStorage.ChainedHeaderAdded -= HandleChainedHeaderAdded;
            this.coreStorage.BlockInvalidated -= HandleBlockInvalidated;
        }

        protected override void SubStart()
        {
            this.NotifyWork();
        }

        protected override void WorkAction()
        {
            var targetBlockLocal = this.targetBlock;
            
            var maxTotalWorkHeader = this.coreStorage.FindMaxTotalWork();
            if (maxTotalWorkHeader != targetBlockLocal)
            {
                if (targetBlockLocal != null
                    && targetBlockLocal.TotalWork == maxTotalWorkHeader.TotalWork
                    && !this.coreStorage.IsBlockInvalid(targetBlockLocal.Hash))
                {
                    // ensure that current winning block remains the same when there is a tie for total work
                }
                else
                {
                    this.targetBlock = maxTotalWorkHeader;

                    var handler = this.TargetBlockChanged;
                    if (handler != null)
                        handler();
                }
            }
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
