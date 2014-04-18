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
        public event Action OnTargetBlockChanged;

        private readonly ChainedHeaderCache chainedHeaderCache;
        private readonly InvalidBlockCache invalidBlockCache;

        private ChainedHeader targetBlock;
        private readonly ReaderWriterLockSlim targetBlockLock;

        private readonly ConcurrentQueue<ChainedHeader> chainedHeaderQueue;

        private readonly AutoResetEvent rescanEvent;

        public TargetBlockWorker(WorkerConfig workerConfig, Logger logger, ChainedHeaderCache chainedHeaderCache, InvalidBlockCache invalidBlockCache)
            : base("TargetBlockWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.chainedHeaderCache = chainedHeaderCache;
            this.invalidBlockCache = invalidBlockCache;

            this.targetBlock = null;
            this.targetBlockLock = new ReaderWriterLockSlim();

            this.chainedHeaderQueue = new ConcurrentQueue<ChainedHeader>();

            this.rescanEvent = new AutoResetEvent(false);

            // wire up cache events
            this.chainedHeaderCache.OnAddition += CheckChainedHeader;

            this.invalidBlockCache.OnAddition += HandleInvalidBlock;
        }

        public ChainedHeader TargetBlock { get { return this.targetBlock; } }

        protected override void SubDispose()
        {
            // cleanup events
            this.chainedHeaderCache.OnAddition -= CheckChainedHeader;

            this.invalidBlockCache.OnAddition -= HandleInvalidBlock;

            this.rescanEvent.Dispose();
        }

        protected override void SubStart()
        {
            this.rescanEvent.Set();
            this.NotifyWork();
        }

        private void CheckChainedHeader(UInt256 blockHash, ChainedHeader chainedHeader)
        {
            try
            {
                if (chainedHeader == null)
                    chainedHeader = this.chainedHeaderCache[blockHash];
            }
            catch (MissingDataException) { return; }

            this.chainedHeaderQueue.Enqueue(chainedHeader);

            this.NotifyWork();
        }

        protected override void WorkAction()
        {
            var currentTargetBlock = this.targetBlock;

            if (this.rescanEvent.WaitOne(0))
            {
                currentTargetBlock = null;
                this.chainedHeaderQueue.EnqueueRange(this.chainedHeaderCache.Values);
            }

            ChainedHeader chainedHeader;
            while (this.chainedHeaderQueue.TryDequeue(out chainedHeader))
            {
                if (this.invalidBlockCache.ContainsKey(chainedHeader.Hash))
                    continue;

                if (currentTargetBlock == null
                    || chainedHeader.TotalWork > currentTargetBlock.TotalWork)
                {
                    currentTargetBlock = chainedHeader;
                }
            }

            if (currentTargetBlock != this.targetBlock)
            {
                this.targetBlock = currentTargetBlock;

                var handler = this.OnTargetBlockChanged;
                if (handler != null)
                    handler();
            }
        }

        private void HandleInvalidBlock(UInt256 blockHash, string data)
        {
            this.rescanEvent.Set();
            this.NotifyWork();
        }
    }
}
