using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using BitSharp.Core.Storage;
using BitSharp.Core.Domain;
using BitSharp.Core;

namespace BitSharp.Esent
{
    public class TransactionStorage : EsentDataStorage<Transaction>, ITransactionStorage
    {
        public TransactionStorage(string baseDirectory)
            : base(baseDirectory, "Transactions",
                tx => DataEncoder.EncodeTransaction(tx),
                (txHash, bytes) => DataEncoder.DecodeTransaction(bytes, txHash))
        { }
    }
}
