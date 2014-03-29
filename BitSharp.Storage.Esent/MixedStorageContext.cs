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
    public class MixedStorageContext : IStorageContext
    {
        private readonly EsentStorageContext esentStorageContext;
        private readonly MemoryStorageContext memoryStorageContext;
        
        private readonly BlockHeaderStorage _blockHeaderStorage;
        private readonly ChainedBlockStorage _chainedBlockStorage;
        private readonly MemoryStorage<UInt256, IImmutableList<UInt256>> _blockTxHashesStorage;
        private readonly MemoryStorage<UInt256, BitSharp.Data.Transaction> _transactionStorage;
        private readonly BlockRollbackStorage _blockRollbackStorage;
        private readonly InvalidBlockStorage _invalidBlockStorage;

        public MixedStorageContext(string baseDirectory, long cacheSizeMaxBytes)
        {
            this.esentStorageContext = new EsentStorageContext(baseDirectory, cacheSizeMaxBytes);
            this.memoryStorageContext = new MemoryStorageContext();

            this._blockHeaderStorage = this.esentStorageContext.BlockHeaderStorage;
            this._chainedBlockStorage = this.esentStorageContext.ChainedBlockStorage;
            this._blockTxHashesStorage = this.memoryStorageContext.BlockTxHashesStorage;
            this._transactionStorage = this.memoryStorageContext.TransactionStorage;
            this._blockRollbackStorage = this.esentStorageContext.BlockRollbackStorage;
            this._invalidBlockStorage = this.esentStorageContext.InvalidBlockStorage;
        }

        public BlockHeaderStorage BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public ChainedBlockStorage ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public MemoryStorage<UInt256, IImmutableList<UInt256>> BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        public MemoryStorage<UInt256, BitSharp.Data.Transaction> Transactionstorage { get { return this._transactionStorage; } }

        public BlockRollbackStorage BlockRollbackStorage { get { return this._blockRollbackStorage; } }

        public IBoundedStorage<UInt256, string> InvalidBlockStorage { get { return this._invalidBlockStorage; } }

        internal string BaseDirectory { get { return this.esentStorageContext.BaseDirectory; } }

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
            //return new MemoryUtxoBuilderStorage(utxo);
            return new UtxoBuilderStorage(utxo);
        }

        public void Dispose()
        {
            new IDisposable[]
            {
                this.esentStorageContext
            }.DisposeList();
        }
    }
}
