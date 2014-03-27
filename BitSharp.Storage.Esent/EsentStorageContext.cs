using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage.Esent;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Esent
{
    public class EsentStorageContext : IStorageContext
    {
        private readonly string baseDirectory;
        private readonly BlockHeaderStorage _blockHeaderStorage;
        private readonly ChainedBlockStorage _chainedBlockStorage;
        private readonly BlockTxHashesStorage _blockTxHashesStorage;
        private readonly TransactionStorage _transactionStorage;
        private readonly BlockRollbackStorage _blockRollbackStorage;
        private readonly InvalidBlockStorage _invalidBlockStorage;

        public EsentStorageContext(string baseDirectory, long cacheSizeMaxBytes)
        {
            var esentAssembly = typeof(PersistentDictionary<string, string>).Assembly;
            var type = esentAssembly.GetType("Microsoft.Isam.Esent.Collections.Generic.CollectionsSystemParameters");
            var method = type.GetMethod("Init");
            method.Invoke(null, null);
            SystemParameters.CacheSizeMax = (cacheSizeMaxBytes / SystemParameters.DatabasePageSize).ToIntChecked();

            this.baseDirectory = baseDirectory;
            this._blockHeaderStorage = new BlockHeaderStorage(this);
            this._chainedBlockStorage = new ChainedBlockStorage(this);
            this._blockTxHashesStorage = new BlockTxHashesStorage(this);
            this._transactionStorage = new TransactionStorage(this);
            this._blockRollbackStorage = new BlockRollbackStorage(this);
            this._invalidBlockStorage = new InvalidBlockStorage(this);
        }

        public BlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public ChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public BlockTxHashesStorage BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        public TransactionStorage Transactionstorage { get { return this._transactionStorage; } }

        public BlockRollbackStorage BlockRollbackStorage { get { return this._blockRollbackStorage; } }

        public IBoundedStorage<UInt256, string> InvalidBlockStorage { get { return this._invalidBlockStorage; } }

        internal string BaseDirectory { get { return this.baseDirectory; } }

        IBoundedStorage<UInt256, BlockHeader> IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBoundedStorage<UInt256, ChainedBlock> IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBoundedStorage<UInt256, IImmutableList<UInt256>> IStorageContext.BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        IUnboundedStorage<UInt256, BitSharp.Data.Transaction> IStorageContext.TransactionStorage { get { return this._transactionStorage; } }

        IBoundedStorage<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>> IStorageContext.BlockRollbackStorage { get { return this._blockRollbackStorage; } }

        IBoundedStorage<UInt256, string> IStorageContext.InvalidBlockStorage { get { return this._invalidBlockStorage; } }

        //public IEnumerable<ChainedBlock> SelectMaxTotalWorkBlocks()
        //{
        //    return this.ChainedBlockStorage.SelectMaxTotalWorkBlocks();
        //}

        public IUtxoBuilderStorage ToUtxoBuilder(IUtxoStorage utxo)
        {
            return new MemoryUtxoBuilderStorage(utxo);
            return new UtxoBuilderStorage(utxo);
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
