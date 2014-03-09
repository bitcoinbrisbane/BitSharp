using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Storage.SqlServer.ExtensionMethods;
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

namespace BitSharp.Storage.SqlServer
{
    public class TransactionStorage : SqlDataStorage, ITransactionStorage
    {
        public TransactionStorage(SqlServerStorageContext storageContext)
            : base(storageContext)
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHash
                    FROM Transactions";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var txHash = reader.GetUInt256(0);
                        yield return txHash;
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<UInt256, Transaction>> ReadAllValues()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHash, TxBytes
                    FROM Transactions";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var txHash = reader.GetUInt256(0);
                        var txBytes = reader.GetBytes(1);

                        yield return new KeyValuePair<UInt256, Transaction>(txHash, StorageEncoder.DecodeTransaction(txBytes.ToMemoryStream(), txHash));
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 txHash, out Transaction transaction)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxBytes
                    FROM Transactions
                    WHERE TxHash = @txHash";

                cmd.Parameters.SetValue("@txHash", SqlDbType.Binary, 32).Value = txHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var txBytes = reader.GetBytes(0);

                        transaction = StorageEncoder.DecodeTransaction(txBytes.ToMemoryStream(), txHash);
                        return true;
                    }
                    else
                    {
                        transaction = default(Transaction);
                        return false;
                    }
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Transaction>>> keyPairs)
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans; 
                
                cmd.Parameters.Add(new SqlParameter { ParameterName = "@txHash", SqlDbType = SqlDbType.Binary, Size = 32 });
                cmd.Parameters.Add(new SqlParameter { ParameterName = "@txBytes", SqlDbType = SqlDbType.Binary });

                cmd.CommandText = CREATE_QUERY;
                foreach (var keyPair in keyPairs.Where(x => x.Value.IsCreate))
                {
                    var transaction = keyPair.Value.Value;

                    var tx = StorageEncoder.EncodeTransaction(transaction);
                    cmd.Parameters["@txHash"].Value = transaction.Hash.ToDbByteArray();
                    cmd.Parameters["@txBytes"].Value = tx;

                    cmd.ExecuteNonQuery();
                }

                cmd.CommandText = UPDATE_QUERY;
                foreach (var keyPair in keyPairs.Where(x => !x.Value.IsCreate))
                {
                    var transaction = keyPair.Value.Value;

                    var txBytes = StorageEncoder.EncodeTransaction(transaction);
                    cmd.Parameters["@txHash"].Value = transaction.Hash.ToDbByteArray();
                    cmd.Parameters["@txBytes"].Value = txBytes;

                    cmd.ExecuteNonQuery();
                }

                trans.Commit();

                return true;
            }
        }

        public void Truncate()
        {
            using (var conn = this.OpenConnection())
            using (var trans = conn.BeginTransaction())
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = trans; 
                
                cmd.CommandText = @"
                    DELETE FROM Transactions";

                cmd.ExecuteNonQuery();

                trans.Commit();
            }
        }

        private const string CREATE_QUERY = @"
            MERGE Transactions AS target
            USING (SELECT @txHash) AS source (TxHash)
            ON (target.TxHash = source.TxHash)
	        WHEN NOT MATCHED THEN
	            INSERT (TxHash, TxBytes)
	            VALUES (@txHash, @txBytes);";

        private const string UPDATE_QUERY = @"
            MERGE Transactions AS target
            USING (SELECT @txHash) AS source (TxHash)
            ON (target.TxHash = source.TxHash)
	        WHEN NOT MATCHED THEN
	            INSERT (TxHash, TxBytes)
	            VALUES (@txHash, @txBytes)
            WHEN MATCHED THEN
                UPDATE SET TxBytes = @txBytes;";
    }
}
