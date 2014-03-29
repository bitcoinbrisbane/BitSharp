using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockHeaderStorage
        : IBoundedStorage<UInt256, BlockHeader> { }

    public interface IChainedBlockStorage :
        IBoundedStorage<UInt256, ChainedBlock> { }

    public interface IBlockTxHashesStorage :
        IBoundedStorage<UInt256, IImmutableList<UInt256>> { }

    public interface ITransactionStorage :
        IUnboundedStorage<UInt256, Transaction> { }

    public interface IBlockRollbackStorage :
        IBoundedStorage<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>> { }

    public interface IInvalidBlockStorage :
        IBoundedStorage<UInt256, string> { }

    public sealed class BlockHeaderCache : PassthroughBoundedCache<UInt256, BlockHeader>
    {
        public BlockHeaderCache(IBoundedCache<UInt256, BlockHeader> cache)
            : base(cache) { }
    }

    public sealed class ChainedBlockCache : PassthroughBoundedCache<UInt256, ChainedBlock>
    {
        public ChainedBlockCache(IBoundedCache<UInt256, ChainedBlock> cache)
            : base(cache) { }
    }

    public sealed class BlockTxHashesCache : PassthroughBoundedCache<UInt256, IImmutableList<UInt256>>
    {
        public BlockTxHashesCache(IBoundedCache<UInt256, IImmutableList<UInt256>> cache)
            : base(cache) { }
    }

    public sealed class TransactionCache : PassthroughUnboundedCache<UInt256, Transaction>
    {
        public TransactionCache(IBoundedCache<UInt256, Transaction> cache)
            : base(cache) { }
    }

    public sealed class BlockRollbackCache : PassthroughBoundedCache<UInt256, Transaction>
    {
        public BlockRollbackCache(IBoundedCache<UInt256, Transaction> cache)
            : base(cache) { }
    }

    public sealed class InvalidBlockCache : PassthroughBoundedCache<UInt256, Transaction>
    {
        public InvalidBlockCache(IBoundedCache<UInt256, Transaction> cache)
            : base(cache) { }
    }

    //public sealed class BlockView : PassthroughBoundedCache<UInt256, Transaction>
    //{
    //    public BlockView(IBoundedCache<UInt256, Transaction> cache)
    //        : base(cache) { }
    //}
}
