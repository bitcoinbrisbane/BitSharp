using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BlockTxHashesCache : BoundedCache<UInt256, IImmutableList<UInt256>>
    {
        private readonly CacheContext _cacheContext;

        public BlockTxHashesCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("BlockTxHashesCache", cacheContext.StorageContext.BlockTxHashesStorage, maxFlushMemorySize, maxCacheMemorySize, blockTxHashes => blockTxHashes.Count * 32)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }
    }
}
