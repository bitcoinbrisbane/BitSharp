using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Test
{
    public class MemoryBlockTxHashesStorage : MemoryStorage<UInt256, IImmutableList<UInt256>>, IBlockTxHashesStorage
    {
        public MemoryBlockTxHashesStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }
    }
}
