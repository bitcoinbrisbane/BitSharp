using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Network;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class MemoryStorageContext : IStorageContext
    {
        private readonly MemoryStorage<UInt256, BlockHeader> _blockHeaderStorage;
        private readonly MemoryStorage<UInt256, ChainedBlock> _chainedBlockStorage;
        private readonly MemoryStorage<UInt256, IImmutableList<UInt256>> _blockTxHashesStorage;
        private readonly MemoryStorage<UInt256, Transaction> _transactionStorage;
        private readonly MemoryStorage<UInt256, string> _invalidBlockStorage;
        private readonly MemoryStorage<NetworkAddressKey, NetworkAddressWithTime> _knownAddressStorage;

        public MemoryStorageContext()
        {
            this._blockHeaderStorage = new MemoryStorage<UInt256, BlockHeader>(this);
            this._chainedBlockStorage = new MemoryStorage<UInt256, ChainedBlock>(this);
            this._blockTxHashesStorage = new MemoryStorage<UInt256, IImmutableList<UInt256>>(this);
            this._transactionStorage = new MemoryStorage<UInt256, Transaction>(this);
            this._invalidBlockStorage = new MemoryStorage<UInt256, string>(this);
            this._knownAddressStorage = new MemoryStorage<NetworkAddressKey, NetworkAddressWithTime>(this);
        }

        public MemoryStorage<UInt256, BlockHeader> BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        public MemoryStorage<UInt256, ChainedBlock> ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        public MemoryStorage<UInt256, IImmutableList<UInt256>> BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        public MemoryStorage<UInt256, Transaction> TransactionStorage { get { return this._transactionStorage; } }

        public MemoryStorage<UInt256, string> InvalidBlockStorage { get { return this._invalidBlockStorage; } }

        public MemoryStorage<NetworkAddressKey, NetworkAddressWithTime> KnownAddressStorage { get { return this._knownAddressStorage; } }

        IBoundedStorage<UInt256, BlockHeader> IStorageContext.BlockHeaderStorage { get { return this._blockHeaderStorage; } }

        IBoundedStorage<UInt256, ChainedBlock> IStorageContext.ChainedBlockStorage { get { return this._chainedBlockStorage; } }

        IBoundedStorage<UInt256, IImmutableList<UInt256>> IStorageContext.BlockTxHashesStorage { get { return this._blockTxHashesStorage; } }

        IUnboundedStorage<UInt256, Transaction> IStorageContext.TransactionStorage { get { return this._transactionStorage; } }

        IBoundedStorage<UInt256, string> IStorageContext.InvalidBlockStorage { get { return this._invalidBlockStorage; } }

        //public IEnumerable<ChainedBlock> SelectMaxTotalWorkBlocks()
        //{
        //    try
        //    {
        //        var maxTotalWork = this.ChainedBlockStorage.Storage.Values.Max(x => x.TotalWork);
        //        return this.ChainedBlockStorage.Storage.Values.Where(x => x.TotalWork == maxTotalWork);
        //    }
        //    catch (InvalidOperationException)
        //    {
        //        return Enumerable.Empty<ChainedBlock>();
        //    }
        //}

        public IUtxoStorageBuilderStorage ToUtxoBuilder(IUtxoStorage utxo)
        {
            return new MemoryUtxoBuilderStorage(utxo);
        }

        public void Dispose()
        {
            new IDisposable[]
            {
                this._chainedBlockStorage,
                //this._blockchainStorage
            }.DisposeList();
        }
    }
}
