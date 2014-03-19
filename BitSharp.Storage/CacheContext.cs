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
    public class CacheContext : ICacheContext
    {
        private readonly IStorageContext storageContext;

        private readonly IBoundedCache<UInt256, BlockHeader> blockHeaderCache;
        private readonly IBoundedCache<UInt256, ChainedBlock> chainedBlockCache;
        private readonly IBoundedCache<UInt256, IImmutableList<UInt256>> blockTxHashesCache;
        private readonly IUnboundedCache<UInt256, Transaction> transactionCache;
        private readonly IUnboundedCache<UInt256, Block> blockView;

        public CacheContext(IStorageContext storageContext)
        {
            this.storageContext = storageContext;

            this.blockHeaderCache = new BoundedFullCache<UInt256, BlockHeader>("Block Header Cache", storageContext.BlockHeaderStorage);
            this.chainedBlockCache = new BoundedFullCache<UInt256, ChainedBlock>("Chained Block Cache", storageContext.ChainedBlockStorage);
            this.blockTxHashesCache = new BoundedCache<UInt256, IImmutableList<UInt256>>("Block TX Hashes Cache", storageContext.BlockTxHashesStorage);
            this.transactionCache = new UnboundedCache<UInt256, Transaction>("Transaction Cache", storageContext.TransactionStorage);
            this.blockView = new BlockView(this);
        }

        public IStorageContext StorageContext { get { return this.storageContext; } }

        public IBoundedCache<UInt256, BlockHeader> BlockHeaderCache { get { return this.blockHeaderCache; } }

        public IBoundedCache<UInt256, ChainedBlock> ChainedBlockCache { get { return this.chainedBlockCache; } }

        public IBoundedCache<UInt256, IImmutableList<UInt256>> BlockTxHashesCache { get { return this.blockTxHashesCache; } }

        public IUnboundedCache<UInt256, Transaction> TransactionCache { get { return this.transactionCache; } }

        public IUnboundedCache<UInt256, Block> BlockView { get { return this.blockView; } }

        public IUtxoBuilderStorage ToUtxoBuilder(Utxo utxo)
        {
            return this.storageContext.ToUtxoBuilder(utxo);
        }

        public IEnumerable<ChainedBlock> SelectMaxTotalWorkBlocks()
        {
            return this.storageContext.SelectMaxTotalWorkBlocks();
        }
    }
}
