using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
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

        public EsentStorageContext()
        {
            this._blockHeaderStorage = new BlockHeaderStorage(this);
            this._blockTxHashesStorage = new BlockTxHashesStorage(this);
            this._transactionStorage = new TransactionStorage(this);
            this._chainedBlockStorage = new ChainedBlockStorage(this);
        }

        public BlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public BlockTxHashesStorage BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        public TransactionStorage Transactionstorage { get { return this._transactionStorage; } }

        public ChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBlockHeaderStorage IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBlockTxHashesStorage IStorageContext.BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        ITransactionStorage IStorageContext.TransactionStorage { get { return this._transactionStorage; } }

        IChainedBlockStorage IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBlockchainStorage IStorageContext.BlockchainStorage { get { return null; } }

        public UtxoBuilder ToUtxoBuilder(Utxo utxo)
        {
            return new PersistentUtxoBuilder(utxo);
        }

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
