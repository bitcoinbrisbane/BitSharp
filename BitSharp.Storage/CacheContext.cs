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
    public class CacheContext
    {
        private readonly IStorageContext _storageContext;

        private readonly BlockStorage _blockStorage;
        private readonly BlockCache _blockCache;
        private readonly BlockHeaderCache _blockHeaderCache;
        private readonly ChainedBlockCache _chainedBlockCache;
        private readonly BlockTxHashesCache _blockTxHashesCache;
        private readonly TransactionCache _transactionCache;

        public CacheContext(IStorageContext storageContext)
        {
            this._storageContext = storageContext;

            this._blockHeaderCache = new BlockHeaderCache(this);
            this._chainedBlockCache = new ChainedBlockCache(this);
            this._blockTxHashesCache = new BlockTxHashesCache(this);
            this._transactionCache = new TransactionCache(this);
            this._blockStorage = new BlockStorage(this);
            this._blockCache = new BlockCache(this);
        }

        public IStorageContext StorageContext { get { return this._storageContext; } }

        public BlockStorage BlockStorage { get { return this._blockStorage; } }

        public BlockCache BlockCache { get { return this._blockCache; } }

        public BlockHeaderCache BlockHeaderCache { get { return this._blockHeaderCache; } }

        public ChainedBlockCache ChainedBlockCache { get { return this._chainedBlockCache; } }

        public BlockTxHashesCache BlockTxHashesCache { get { return this._blockTxHashesCache; } }

        public TransactionCache TransactionCache { get { return this._transactionCache; } }
    }
}
