using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BlockTotalWorkCache : BoundedCache<UInt256, BigInteger>
    {
        private readonly CacheContext _cacheContext;

        private BigInteger maxTotalWork;
        private ImmutableList<UInt256> maxTotalWorkBlocks;
        private readonly ReaderWriterLockSlim maxTotalWorkLock;

        public BlockTotalWorkCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("BlockTotalWorkCache", cacheContext.StorageContext.BlockTotalWorkStorage, maxFlushMemorySize, maxCacheMemorySize, sizeEstimator: x => 64)
        {
            this._cacheContext = cacheContext;
            this.maxTotalWork = -1;
            this.maxTotalWorkBlocks = ImmutableList.Create<UInt256>();
            this.maxTotalWorkLock = new ReaderWriterLockSlim();

            var checkThread = new Thread(() =>
            {
                foreach (var keyPair in this.StorageContext.BlockTotalWorkStorage.SelectMaxTotalWorkBlocks())
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

        public override void CreateValue(UInt256 blockHash, BigInteger totalWork)
        {
            CheckTotalWork(blockHash, totalWork);
            base.CreateValue(blockHash, totalWork);
        }

        public override void UpdateValue(UInt256 blockHash, BigInteger totalWork)
        {
            CheckTotalWork(blockHash, totalWork);
            base.UpdateValue(blockHash, totalWork);
        }

        private void CheckTotalWork(UInt256 blockHash, BigInteger totalWork)
        {
            this.maxTotalWorkLock.DoWrite(() =>
            {
                if (totalWork > this.maxTotalWork)
                {
                    this.maxTotalWork = totalWork;
                    this.maxTotalWorkBlocks = ImmutableList.Create<UInt256>(blockHash);
                }
                else if (totalWork == this.maxTotalWork)
                {
                    this.maxTotalWorkBlocks = this.maxTotalWorkBlocks.Add(blockHash);
                }
            });
        }
    }
}
