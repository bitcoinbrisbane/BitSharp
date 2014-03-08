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
    public class MemoryTransactionStorage : MemoryStorage<UInt256, Transaction>, ITransactionStorage
    {
        public MemoryTransactionStorage(MemoryStorageContext storageContext)
            : base(storageContext)
        { }
    }
}
