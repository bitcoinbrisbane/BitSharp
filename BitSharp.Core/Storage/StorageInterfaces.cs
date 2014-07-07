using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IBlockHeaderStorage
        : IBoundedStorage<UInt256, BlockHeader> { }

    public interface IChainedHeaderStorage :
        IBoundedStorage<UInt256, ChainedHeader> { }

    public interface IInvalidBlockStorage :
        IBoundedStorage<UInt256, string> { }

    public sealed class BlockHeaderCache : PassthroughBoundedCache<UInt256, BlockHeader>
    {
        public BlockHeaderCache(IBoundedCache<UInt256, BlockHeader> cache)
            : base(cache) { }
    }

    public sealed class ChainedHeaderCache : PassthroughBoundedCache<UInt256, ChainedHeader>
    {
        public ChainedHeaderCache(IBoundedCache<UInt256, ChainedHeader> cache)
            : base(cache) { }
    }

    public sealed class InvalidBlockCache : PassthroughBoundedCache<UInt256, string>
    {
        public InvalidBlockCache(IBoundedCache<UInt256, string> cache)
            : base(cache) { }
    }
}
