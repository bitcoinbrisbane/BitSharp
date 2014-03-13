using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
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

namespace BitSharp.Storage
{
    public class ChainedBlockCache : BoundedCache<UInt256, ChainedBlock>
    {
        private readonly CacheContext _cacheContext;

        private BigInteger maxTotalWork;
        private ImmutableList<UInt256> maxTotalWorkBlocks;
        private readonly ReaderWriterLockSlim maxTotalWorkLock;

        public ChainedBlockCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("ChainedBlockCache", cacheContext.StorageContext.ChainedBlockStorage, maxFlushMemorySize, maxCacheMemorySize, ChainedBlock.SizeEstimator)
        {
            this._cacheContext = cacheContext;

            this.maxTotalWork = -1;
            this.maxTotalWorkBlocks = ImmutableList.Create<UInt256>();
            this.maxTotalWorkLock = new ReaderWriterLockSlim();

            //TODO periodic rescan
            var checkThread = new Thread(() =>
            {
                foreach (var keyPair in this._cacheContext.StorageContext.ChainedBlockStorage.SelectMaxTotalWorkBlocks())
                    CheckTotalWork(keyPair.Key, keyPair.Value);
            });
            checkThread.Start();
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public IImmutableList<UInt256> MaxTotalWorkBlocks
        {
            get
            {
                return this.maxTotalWorkLock.DoRead(() => this.maxTotalWorkBlocks.ToImmutableList());
            }
        }

        public override void CreateValue(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            base.CreateValue(blockHash, chainedBlock);
            CheckTotalWork(blockHash, chainedBlock);
        }

        public override void UpdateValue(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            base.UpdateValue(blockHash, chainedBlock);
            CheckTotalWork(blockHash, chainedBlock);
        }

        private void CheckTotalWork(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            this.maxTotalWorkLock.DoWrite(() =>
            {
                if (chainedBlock.TotalWork > this.maxTotalWork)
                {
                    this.maxTotalWork = chainedBlock.TotalWork;
                    this.maxTotalWorkBlocks = ImmutableList.Create<UInt256>(blockHash);
                }
                else if (chainedBlock.TotalWork == this.maxTotalWork)
                {
                    this.maxTotalWorkBlocks = this.maxTotalWorkBlocks.Add(blockHash);
                }
            });
        }
    }
}