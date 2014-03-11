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
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BlockTotalWorkCache : BoundedCache<UInt256, BigInteger>
    {
        private readonly CacheContext _cacheContext;

        private IImmutableList<UInt256> maxTotalWorkBlocks;

        public BlockTotalWorkCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("BlockTotalWorkCache", cacheContext.StorageContext.BlockTotalWorkStorage, maxFlushMemorySize, maxCacheMemorySize, sizeEstimator: x => 64)
        {
            this._cacheContext = cacheContext;
            this.maxTotalWorkBlocks = null;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public IImmutableList<UInt256> MaxTotalWorkBlocks
        {
            get
            {
                //TODO
                //this.maxTotalWorkBlocks

                return this.StorageContext.BlockTotalWorkStorage.SelectMaxTotalWorkBlocks().ToImmutableList();
            }
        }
    }
}
