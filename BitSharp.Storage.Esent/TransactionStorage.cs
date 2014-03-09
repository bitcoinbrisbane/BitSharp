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
    public class TransactionStorage : EsentDataStorage, ITransactionStorage
    {
        public TransactionStorage(EsentStorageContext storageContext)
            : base(storageContext, "transactions")
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.Data.Keys.Select(x => new UInt256(x));
        }

        public IEnumerable<KeyValuePair<UInt256, Transaction>> ReadAllValues()
        {
            return this.Data.Select(x =>
                new KeyValuePair<UInt256, Transaction>(new UInt256(x.Key), StorageEncoder.DecodeTransaction(x.Value.ToMemoryStream())));
        }

        public bool TryReadValue(UInt256 txHash, out Transaction transaction)
        {
            byte[] txBytes;
            if (this.Data.TryGetValue(txHash.ToByteArray(), out txBytes))
            {
                transaction = StorageEncoder.DecodeTransaction(txBytes.ToMemoryStream(), txHash);
                return true;
            }
            else
            {
                transaction = default(Transaction);
                return false;
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Transaction>>> keyPairs)
        {
            foreach (var keyPair in keyPairs)
                this.Data[keyPair.Key.ToByteArray()] = StorageEncoder.EncodeTransaction(keyPair.Value.Value);

            return true;
        }

        public void Truncate()
        {
            this.Data.Clear();
        }
    }
}
