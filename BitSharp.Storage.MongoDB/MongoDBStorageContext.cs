using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage.Esent;
using BitSharp.Storage.MongoDB;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.MongoDB
{
    public class MongoDBStorageContext : IStorageContext
    {
        private readonly MongoClient client;
        private readonly MongoServer server;
        private readonly MongoDatabase database;
        private readonly string baseDirectory;
        private readonly BlockHeaderStorage _blockHeaderStorage;
        private readonly ChainedBlockStorage _chainedBlockStorage;
        private readonly BlockTxHashesStorage _blockTxHashesStorage;
        private readonly TransactionStorage _transactionStorage;
        private readonly InvalidBlockStorage _invalidBlockStorage;

        public MongoDBStorageContext(string baseDirectory, long cacheSizeMaxBytes)
        {
            var settings = MongoClientSettings.FromUrl(new MongoUrl("mongodb://localhost"));
            //settings.WriteConcern.W = 0;
            //settings.WriteConcern.FSync = false;
            //settings.WriteConcern.Journal = false;
            this.client = new MongoClient(settings);
            this.server = client.GetServer();
            this.database = server.GetDatabase("BitSharp");

            this.baseDirectory = baseDirectory;
            this._blockHeaderStorage = new BlockHeaderStorage(this);
            this._chainedBlockStorage = new ChainedBlockStorage(this);
            this._blockTxHashesStorage = new BlockTxHashesStorage(this);
            this._transactionStorage = new TransactionStorage(this);
            this._invalidBlockStorage = new InvalidBlockStorage(this);
        }

        public BlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public ChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public BlockTxHashesStorage BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        public TransactionStorage Transactionstorage { get { return this._transactionStorage; } }

        public IBoundedStorage<UInt256, string> InvalidBlockStorage { get { return this._invalidBlockStorage; } }

        internal string BaseDirectory { get { return this.baseDirectory; } }

        internal MongoClient Client { get { return this.client; } }

        internal MongoServer Server { get { return this.server; } }

        internal MongoDatabase Database { get { return this.database; } }

        IBoundedStorage<UInt256, BlockHeader> IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBoundedStorage<UInt256, ChainedBlock> IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBoundedStorage<UInt256, IImmutableList<UInt256>> IStorageContext.BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        IUnboundedStorage<UInt256, BitSharp.Data.Transaction> IStorageContext.TransactionStorage { get { return this._transactionStorage; } }

        IBoundedStorage<UInt256, string> IStorageContext.InvalidBlockStorage { get { return this._invalidBlockStorage; } }

        //public IEnumerable<ChainedBlock> SelectMaxTotalWorkBlocks()
        //{
        //    return this.ChainedBlockStorage.SelectMaxTotalWorkBlocks();
        //}

        public IUtxoBuilderStorage ToUtxoBuilder(IUtxoStorage utxo)
        {
            return new PersistentUtxoBuilderStorage(utxo);
        }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._blockHeaderStorage,
                this._chainedBlockStorage,
                this._blockTxHashesStorage,
                this._transactionStorage
            }.DisposeList();
        }
    }
}
