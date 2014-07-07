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
        private readonly MemoryBlockHeaderStorage blockHeaderStorage;
        private readonly MemoryChainedHeaderStorage chainedHeaderStorage;
        private readonly MemoryInvalidBlockStorage invalidBlockStorage;
        private readonly MemoryBlockStorageNew blockStorage;

        public MemoryStorageManager()
        {
            this.blockHeaderStorage = new MemoryBlockHeaderStorage();
            this.chainedHeaderStorage = new MemoryChainedHeaderStorage();
            this.invalidBlockStorage = new MemoryInvalidBlockStorage();
            this.blockStorage = new MemoryBlockStorageNew();
        }

        public void Dispose()
        {
        }

        public IBlockHeaderStorage BlockHeaderStorage
        {
            get { return this.blockHeaderStorage; }
        }

        public IChainedHeaderStorage ChainedHeaderStorage
        {
            get { return this.chainedHeaderStorage; }
        }

        public IInvalidBlockStorage InvalidBlockStorage
        {
            get { return this.invalidBlockStorage; }
        }

        public IBlockStorageNew BlockStorage
        {
            get { return this.blockStorage; }
        }

        public IChainStateBuilderStorage CreateOrLoadChainState(ChainedHeader genesisHeader)
        {
            return new MemoryChainStateBuilderStorage(genesisHeader);
        }
    }
}
