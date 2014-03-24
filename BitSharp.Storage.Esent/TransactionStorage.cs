using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Network;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Data;
using System.Data.SqlClient;

namespace BitSharp.Storage.Esent
{
    public class TransactionStorage : EsentDataStorage<Transaction>
    {
        public TransactionStorage(EsentStorageContext storageContext)
            : base(storageContext, "transactions",
                tx => StorageEncoder.EncodeTransaction(tx),
                (txHash, bytes) => StorageEncoder.DecodeTransaction(bytes, txHash))
        { }
    }
}
