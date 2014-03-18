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

        public IEnumerable<ChainedBlock> SelectMaxTotalWorkBlocks()
        {
            try
            {
                var maxTotalWork = this.Storage.Values.Max(x => x.TotalWork);
                return this.Storage.Values.Where(x => x.TotalWork == maxTotalWork);
            }
            catch (InvalidOperationException)
            {
                return Enumerable.Empty<ChainedBlock>();
            }
        }
    }
}