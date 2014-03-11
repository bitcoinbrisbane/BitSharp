using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.Esent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Esent
{
    public class EsentStorageContext : IStorageContext
    {
        private readonly BlockHeaderStorage _blockHeaderStorage;
        private readonly BlockTxHashesStorage _blockTxHashesStorage;
        private readonly TransactionStorage _transactionStorage;
        private readonly ChainedBlockStorage _chainedBlockStorage;
        private readonly BlockTotalWorkStorage _blockTotalWorkStorage;

        public EsentStorageContext()
        {
            this._blockHeaderStorage = new BlockHeaderStorage(this);
            this._blockTxHashesStorage = new BlockTxHashesStorage(this);
            this._transactionStorage = new TransactionStorage(this);
            this._chainedBlockStorage = new ChainedBlockStorage(this);
            this._blockTotalWorkStorage = new BlockTotalWorkStorage(this);
        }

        public BlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public BlockTxHashesStorage BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        public TransactionStorage Transactionstorage { get { return this._transactionStorage; } }

        public ChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public BlockTotalWorkStorage BlockTotalWorkStorage { get { return this._blockTotalWorkStorage; } }

        IBlockHeaderStorage IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBlockTxHashesStorage IStorageContext.BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        ITransactionStorage IStorageContext.TransactionStorage { get { return this._transactionStorage; } }

        IChainedBlockStorage IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBlockTotalWorkStorage IStorageContext.BlockTotalWorkStorage { get { return this._blockTotalWorkStorage; } }

        IBlockchainStorage IStorageContext.BlockchainStorage { get { return null; } }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._blockHeaderStorage,
                this._blockTxHashesStorage,
                this._chainedBlockStorage
            }.DisposeList();
        }
    }
}
