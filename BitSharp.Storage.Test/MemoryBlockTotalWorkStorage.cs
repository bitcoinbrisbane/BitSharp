using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryBlockTotalWorkStorage : MemoryStorage<UInt256, BigInteger>, IBlockTotalWorkStorage
    {
        public MemoryBlockTotalWorkStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<KeyValuePair<UInt256, BigInteger>> SelectMaxTotalWorkBlocks()
        {
            throw new NotImplementedException();
        }
    }
}
