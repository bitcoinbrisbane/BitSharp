using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryStorageManager : IStorageManager
    {
        private readonly MemoryBlockStorage blockStorage;
        private readonly MemoryBlockTxesStorage blockTxesStorage;

        public MemoryStorageManager()
        {
            this.blockStorage = new MemoryBlockStorage();
            this.blockTxesStorage = new MemoryBlockTxesStorage();
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
            return new MemoryChainStateCursor();
        }
    }
}
