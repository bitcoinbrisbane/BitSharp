using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryChainedBlockStorage : MemoryStorage<UInt256, ChainedBlock>, IChainedBlockStorage
    {
        public MemoryChainedBlockStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<KeyValuePair<UInt256, ChainedBlock>> SelectMaxTotalWorkBlocks()
        {
            var maxTotalWork = this.Storage.Max(x => x.Value.TotalWork);
            return this.Storage.Where(x => x.Value.TotalWork == maxTotalWork);
        }
    }
}