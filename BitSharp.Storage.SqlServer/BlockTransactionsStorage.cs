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
using System.Collections.Immutable;
using System.IO;

namespace BitSharp.Storage.SqlServer
{
    public class BlockTransactionsStorage : SqlDataStorage, IBlockTransactionsStorage
    {
        public BlockTransactionsStorage(SqlServerStorageContext storageContext)
            : base(storageContext)
        { }

        public bool TryReadValue(UInt256 blockHash, out ImmutableArray<Transaction> blockTransactions)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHash, TxBytes
                    FROM BlockTransactions
                    WHERE BlockHash = @blockHash
                    ORDER BY TxIndex ASC";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    var blockTransactionsBuilder = ImmutableArray.CreateBuilder<Transaction>();

                    while (reader.Read())
                    {
                        var txHash = reader.GetUInt256(0);
                        var txBytes = reader.GetBytes(1);

                        blockTransactionsBuilder.Add(StorageEncoder.DecodeTransaction(txBytes.ToMemoryStream(), txHash));
                    }

                    blockTransactions = blockTransactionsBuilder.ToImmutable();
                    return blockTransactions.Length > 0;
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<ImmutableArray<Transaction>>>> values)
        {
            var stopwatch = new Stopwatch();
            var count = 0;
            try
            {
                using (var conn = this.OpenConnection())
                using (var trans = conn.BeginTransaction())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;

                    cmd.CommandText = @"
                        MERGE BlockTransactions AS target
                        USING (SELECT @blockHash, @txIndex) AS source (BlockHash, TxIndex)
                        ON (target.BlockHash = source.BlockHash AND target.TxIndex = source.TxIndex)
	                    WHEN NOT MATCHED THEN
	                        INSERT (BlockHash, TxIndex, TxHash, TxBytes)
	                        VALUES (@blockHash, @txIndex, @txHash, @txBytes);";

                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@txIndex", DbType = DbType.Int32 });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@txHash", DbType = DbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@txBytes", DbType = DbType.Binary });

                    foreach (var keyPair in values)
                    {
                        var blockHash = keyPair.Key;

                        cmd.Parameters["@blockHash"].Value = blockHash.ToDbByteArray();

                        for (var txIndex = 0; txIndex < keyPair.Value.Value.Length; txIndex++)
                        {
                            var tx = keyPair.Value.Value[txIndex];
                            var txBytes = StorageEncoder.EncodeTransaction(tx);

                            cmd.Parameters["@txIndex"].Value = txIndex;
                            cmd.Parameters["@txHash"].Value = tx.Hash.ToDbByteArray();
                            cmd.Parameters["@txBytes"].Size = txBytes.Length;
                            cmd.Parameters["@txBytes"].Value = txBytes;

                            count++;
                            cmd.ExecuteNonQuery();
                        }
                    }

                    stopwatch.Start();
                    trans.Commit();
                    stopwatch.Stop();

                    return true;
                }
            }
            finally
            {
                //Debug.WriteLine("flushed {0,5}: {1:#,##0.000000}s @ {2:#,##0.000}/s".Format2(count, stopwatch.ElapsedSecondsFloat(), count / stopwatch.ElapsedSecondsFloat()));
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
                    DELETE FROM BlockTransactions";

                cmd.ExecuteNonQuery();

                trans.Commit();
            }
        }

        public IEnumerable<UInt256> ReadAllBlockHashes()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT DISTINCT BlockHash
                    FROM BlockTransactions";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        yield return blockHash;
                    }
                }
            }
        }

        public bool TryReadTransaction(TxKey txKey, out Transaction transaction)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHash, TxBytes
                    FROM BlockTransactions
                    WHERE BlockHash = @blockHash AND TxIndex = @txIndex";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = txKey.BlockHash.ToDbByteArray();
                cmd.Parameters.SetValue("@txIndex", SqlDbType.Int).Value = txKey.TxIndex.ToIntChecked();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var txHash = reader.GetUInt256(0);
                        var txBytes = reader.GetBytes(1);

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
    }
}
