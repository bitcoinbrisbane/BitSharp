using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryStorageContext : IStorageContext
    {
        private readonly MemoryBlockHeaderStorage _blockHeaderStorage;
        private readonly MemoryBlockTxHashesStorage _blockTxHashesStorage;
        private readonly MemoryTransactionStorage _transactionStorage;
        private readonly MemoryChainedBlockStorage _chainedBlockStorage;
        private readonly MemoryBlockTotalWorkStorage _blockTotalWorkStorage;
        private readonly MemoryBlockchainStorage _blockchainStorage;

        public MemoryStorageContext()
        {
            this._blockHeaderStorage = new MemoryBlockHeaderStorage(this);
            this._blockTxHashesStorage = new MemoryBlockTxHashesStorage(this);
            this._transactionStorage = new MemoryTransactionStorage(this);
            this._chainedBlockStorage = new MemoryChainedBlockStorage(this);
            this._blockTotalWorkStorage = new MemoryBlockTotalWorkStorage(this);
            this._blockchainStorage = new MemoryBlockchainStorage(this);
        }

        public MemoryBlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public MemoryBlockTxHashesStorage BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        public MemoryTransactionStorage TransactionStorage { get { return this._transactionStorage; } }

        public MemoryChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public MemoryBlockTotalWorkStorage BlockTotalWorkStorage { get { return this._blockTotalWorkStorage; } }

        public MemoryBlockchainStorage BlockchainStorage { get { return this._blockchainStorage; } }

        IBlockHeaderStorage IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBlockTxHashesStorage IStorageContext.BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        ITransactionStorage IStorageContext.TransactionStorage { get { return this._transactionStorage; } }

        IChainedBlockStorage IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBlockTotalWorkStorage IStorageContext.BlockTotalWorkStorage { get { return this._blockTotalWorkStorage; } }

        IBlockchainStorage IStorageContext.BlockchainStorage { get { return this._blockchainStorage; } }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._chainedBlockStorage,
                this._blockchainStorage
            }.DisposeList();
        }
    }
}
