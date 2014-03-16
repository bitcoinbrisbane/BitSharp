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
        private readonly CacheContext cacheContext;

        public ChainedBlockCache(CacheContext cacheContext)
            : base("ChainedBlockCache", cacheContext.StorageContext.ChainedBlockStorage)
        {
            this.cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this.cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }
   }
}