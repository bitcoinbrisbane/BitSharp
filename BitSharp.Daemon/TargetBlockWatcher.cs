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

        private readonly CacheContext cacheContext;
        private ChainedBlock targetBlock;
        private readonly ReaderWriterLockSlim targetBlockLock;

        public TargetBlockWatcher(CacheContext cacheContext)
        {
            this.cacheContext = cacheContext;

            this.targetBlock = null;
            this.targetBlockLock = new ReaderWriterLockSlim();

            // wire up cache events
            this.cacheContext.ChainedBlockCache.OnAddition += CheckChainedBlock;
            this.cacheContext.ChainedBlockCache.OnModification += CheckChainedBlock;

            CheckAllChainedBlocks();
        }

        public ChainedBlock TargetBlock { get { return this.targetBlock; } }

        public void Dispose()
        {
            // cleanup events
            this.cacheContext.ChainedBlockCache.OnAddition -= CheckChainedBlock;
            this.cacheContext.ChainedBlockCache.OnModification -= CheckChainedBlock;
        }

        private void CheckAllChainedBlocks()
        {
            //TODO periodic rescan
            //new Thread(() =>
            //    {
            new MethodTimer().Time(() =>
            {
                foreach (var chainedBlock in this.cacheContext.StorageContext.SelectMaxTotalWorkBlocks())
                {
                    //TODO
                    // cooperative loop
                    //if (this.shutdownToken.IsCancellationRequested)
                    //    return;

                    CheckChainedBlock(chainedBlock.BlockHash, chainedBlock);
                }
            });

            //Debugger.Break();
            //}).start();
        }

        private void CheckChainedBlock(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            try
            {
                if (chainedBlock == null)
                    chainedBlock = this.cacheContext.ChainedBlockCache[blockHash];
            }
            catch (MissingDataException) { return; }

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
    }
}
