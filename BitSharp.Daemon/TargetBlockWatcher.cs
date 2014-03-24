using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon
{
    internal class TargetBlockWatcher : IDisposable
    {
        public event Action OnTargetBlockChanged;

        private readonly ICacheContext cacheContext;
        private ChainedBlock targetBlock;
        private readonly ReaderWriterLockSlim targetBlockLock;

        private readonly WorkerMethod rescanWorker;

        public TargetBlockWatcher(ICacheContext cacheContext)
        {
            this.cacheContext = cacheContext;

            this.targetBlock = null;
            this.targetBlockLock = new ReaderWriterLockSlim();

            this.rescanWorker = new WorkerMethod("RescanWorker", CheckAllChainedBlocks, initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue);

            // wire up cache events
            this.cacheContext.ChainedBlockCache.OnAddition += CheckChainedBlock;

            this.cacheContext.InvalidBlockCache.OnAddition += HandleInvalidBlock;
        }

        public ChainedBlock TargetBlock { get { return this.targetBlock; } }

        public void Dispose()
        {
            // cleanup events
            this.cacheContext.ChainedBlockCache.OnAddition -= CheckChainedBlock;

            this.cacheContext.InvalidBlockCache.OnAddition -= HandleInvalidBlock;

            this.rescanWorker.Dispose();
        }

        public void Start()
        {
            this.rescanWorker.Start();
            this.rescanWorker.NotifyWork();
        }

        private void CheckAllChainedBlocks()
        {
            new MethodTimer().Time(() =>
            {
                this.targetBlockLock.DoWrite(() =>
                {
                    this.targetBlock = null;
                });

                foreach (var chainedBlock in this.cacheContext.ChainedBlockCache.Values)
                {
                    //TODO
                    // cooperative loop
                    //if (this.shutdownToken.IsCancellationRequested)
                    //    return;

                    CheckChainedBlock(chainedBlock.BlockHash, chainedBlock);
                }
            });
        }

        private void CheckChainedBlock(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            try
            {
                if (chainedBlock == null)
                    chainedBlock = this.cacheContext.ChainedBlockCache[blockHash];
            }
            catch (MissingDataException) { return; }

            if (this.cacheContext.InvalidBlockCache.ContainsKey(chainedBlock.BlockHash))
                return;

            var wasChanged = false;
            this.targetBlockLock.DoWrite(() =>
            {
                if (this.targetBlock == null
                    || chainedBlock.TotalWork > this.targetBlock.TotalWork)
                {
                    this.targetBlock = chainedBlock;
                    wasChanged = true;
                }
            });

            if (wasChanged)
            {
                var handler = this.OnTargetBlockChanged;
                if (handler != null)
                    handler();
            }
        }

        private void HandleInvalidBlock(UInt256 blockHash, string data)
        {
            this.rescanWorker.NotifyWork();
        }
    }
}
