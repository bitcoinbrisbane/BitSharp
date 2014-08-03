using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryStorageManager : IStorageManager
    {
        private readonly MemoryBlockStorage blockStorage;
        private readonly MemoryBlockTxesStorage blockTxesStorage;
        private readonly MemoryChainStateStorage chainStateStorage;

        public MemoryStorageManager()
            : this(null, null, null)
        { }

        internal MemoryStorageManager(Chain chain = null, ImmutableSortedDictionary<UInt256, UnspentTx> unspentTransactions = null, ImmutableDictionary<int, IImmutableList<SpentTx>> spentTransactions = null)
        {
            this.blockStorage = new MemoryBlockStorage();
            this.blockTxesStorage = new MemoryBlockTxesStorage();
            this.chainStateStorage = new MemoryChainStateStorage(chain, unspentTransactions, spentTransactions);
        }

        public void Dispose()
        {
        }

        public IBlockStorage BlockStorage
        {
            get { return this.blockStorage; }
        }

        public IBlockTxesStorage BlockTxesStorage
        {
            get { return this.blockTxesStorage; }
        }

        public IChainStateCursor OpenChainStateCursor()
        {
            return new MemoryChainStateCursor(this.chainStateStorage);
        }
    }
}
