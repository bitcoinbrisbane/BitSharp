using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon
{
    internal class TargetBlockWorker : Worker
    {
        public event Action OnTargetBlockChanged;

        private readonly ICacheContext cacheContext;
        private ChainedBlock targetBlock;
        private readonly ReaderWriterLockSlim targetBlockLock;

        private readonly ConcurrentQueue<ChainedBlock> chainedBlockQueue;

        private readonly AutoResetEvent rescanEvent;

        public TargetBlockWorker(ICacheContext cacheContext, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base("TargetBlockWorker", initialNotify, minIdleTime, maxIdleTime)
        {
            this.cacheContext = cacheContext;

            this.targetBlock = null;
            this.targetBlockLock = new ReaderWriterLockSlim();

            this.chainedBlockQueue = new ConcurrentQueue<ChainedBlock>();

            this.rescanEvent = new AutoResetEvent(false);

            // wire up cache events
            this.cacheContext.ChainedBlockCache.OnAddition += CheckChainedBlock;

            this.cacheContext.InvalidBlockCache.OnAddition += HandleInvalidBlock;
        }

        public ChainedBlock TargetBlock { get { return this.targetBlock; } }

        protected override void SubDispose()
        {
            // cleanup events
            this.cacheContext.ChainedBlockCache.OnAddition -= CheckChainedBlock;

            this.cacheContext.InvalidBlockCache.OnAddition -= HandleInvalidBlock;

            this.rescanEvent.Dispose();
        }

        protected override void SubStart()
        {
            this.rescanEvent.Set();
            this.NotifyWork();
        }

        private void CheckChainedBlock(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            try
            {
                if (chainedBlock == null)
                    chainedBlock = this.cacheContext.ChainedBlockCache[blockHash];
            }
            catch (MissingDataException) { return; }

            this.chainedBlockQueue.Enqueue(chainedBlock);

            this.NotifyWork();
        }

        protected override void WorkAction()
        {
            var currentTargetBlock = this.targetBlock;

            if (this.rescanEvent.WaitOne(0))
            {
                currentTargetBlock = null;
                this.chainedBlockQueue.EnqueueRange(this.cacheContext.ChainedBlockCache.Values);
            }

            ChainedBlock chainedBlock;
            while (this.chainedBlockQueue.TryDequeue(out chainedBlock))
            {
                if (this.cacheContext.InvalidBlockCache.ContainsKey(chainedBlock.BlockHash))
                    continue;

                if (currentTargetBlock == null
                    || chainedBlock.TotalWork > currentTargetBlock.TotalWork)
                {
                    currentTargetBlock = chainedBlock;
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
