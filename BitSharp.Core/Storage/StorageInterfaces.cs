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

    public interface IBlockStorage :
        IBoundedStorage<UInt256, Block> { }

    public interface IBlockTxHashesStorage :
        IBoundedStorage<UInt256, IImmutableList<UInt256>> { }

    public interface ITransactionStorage :
        IUnboundedStorage<UInt256, Transaction> { }

    public interface ISpentTransactionsStorage :
        IBoundedStorage<UInt256, IImmutableList<KeyValuePair<UInt256, SpentTx>>> { }

    public interface ISpentOutputsStorage :
        IBoundedStorage<UInt256, IImmutableList<KeyValuePair<TxOutputKey, TxOutput>>> { }

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

    //public interface BlockCache : IBlockStorageNew { }
    //public sealed class BlockCache : PassthroughBoundedCache<UInt256, Block>
    //{
    //    public BlockCache(IBoundedCache<UInt256, Block> cache)
    //        : base(cache) { }
    //}

    public sealed class BlockTxHashesCache : PassthroughBoundedCache<UInt256, IImmutableList<UInt256>>
    {
        public BlockTxHashesCache(IBoundedCache<UInt256, IImmutableList<UInt256>> cache)
            : base(cache) { }
    }

    public sealed class TransactionCache : PassthroughUnboundedCache<UInt256, Transaction>
    {
        public TransactionCache(IUnboundedCache<UInt256, Transaction> cache)
            : base(cache) { }
    }

    public sealed class SpentTransactionsCache : PassthroughBoundedCache<UInt256, IImmutableList<KeyValuePair<UInt256, SpentTx>>>
    {
        public SpentTransactionsCache(IBoundedCache<UInt256, IImmutableList<KeyValuePair<UInt256, SpentTx>>> cache)
            : base(cache) { }
    }

    public sealed class SpentOutputsCache : PassthroughBoundedCache<UInt256, IImmutableList<KeyValuePair<TxOutputKey, TxOutput>>>
    {
        public SpentOutputsCache(IBoundedCache<UInt256, IImmutableList<KeyValuePair<TxOutputKey, TxOutput>>> cache)
            : base(cache) { }
    }

    public sealed class InvalidBlockCache : PassthroughBoundedCache<UInt256, string>
    {
        public InvalidBlockCache(IBoundedCache<UInt256, string> cache)
            : base(cache) { }
    }
}
