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

        public IEnumerable<UInt256> ReadAllKeys()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash
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

        public IEnumerable<KeyValuePair<UInt256, ImmutableList<UInt256>>> ReadAllValues()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT BlockHash, TxHashesBytes
                    FROM BlockTransactions";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blockHash = reader.GetUInt256(0);
                        var txHashesBytes = reader.GetBytes(1);

                        var txHashes = ImmutableList.CreateBuilder<UInt256>();
                        var txHashBytes = new byte[32];
                        for (var i = 0; i < txHashesBytes.Length; i += 32)
                        {
                            Buffer.BlockCopy(txHashesBytes, i, txHashBytes, 0, 32);
                            txHashes.Add(new UInt256(txHashBytes));
                        }

                        yield return new KeyValuePair<UInt256, ImmutableList<UInt256>>(blockHash, txHashes.ToImmutable());
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 blockHash, out ImmutableList<UInt256> blockTxHashes)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHashesBytes
                    FROM BlockTransactions
                    WHERE BlockHash = @blockHash";

                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = blockHash.ToDbByteArray();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var txHashesBytes = reader.GetBytes(0);

                        var txHashes = ImmutableList.CreateBuilder<UInt256>();
                        var txHashBytes = new byte[32];
                        for (var i = 0; i < txHashesBytes.Length; i += 32)
                        {
                            Buffer.BlockCopy(txHashesBytes, i, txHashBytes, 0, 32);
                            txHashes.Add(new UInt256(txHashBytes));
                        }

                        blockTxHashes = txHashes.ToImmutable();
                        return true;
                    }
                    else
                    {
                        blockTxHashes = default(ImmutableList<UInt256>);
                        return false;
                    }
                }
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<ImmutableList<UInt256>>>> keyPairs)
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
                        USING (SELECT @blockHash) AS source (BlockHash)
                        ON (target.BlockHash = source.BlockHash)
	                    WHEN NOT MATCHED THEN
	                        INSERT (BlockHash, TxHashesBytes)
	                        VALUES (@blockHash, @txHashesBytes);";

                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@blockHash", DbType = DbType.Binary, Size = 32 });
                    cmd.Parameters.Add(new SqlParameter { ParameterName = "@txHashesBytes", DbType = DbType.Binary });

                    foreach (var keyPair in keyPairs)
                    {
                        var blockHash = keyPair.Key;

                        var txHashesBytes = new byte[keyPair.Value.Value.Count * 32];
                        for (var i = 0; i < keyPair.Value.Value.Count; i++)
                        {
                            Buffer.BlockCopy(keyPair.Value.Value[i].ToByteArray(), 0, txHashesBytes, i * 32, 32);
                        }

                        cmd.Parameters["@blockHash"].Value = blockHash.ToDbByteArray();
                        cmd.Parameters["@txHashesBytes"].Value = txHashesBytes;

                        count++;
                        cmd.ExecuteNonQuery();
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

        //        public IEnumerable<UInt256> ReadAllBlockHashes()
        //        {
        //            using (var conn = this.OpenConnection())
        //            using (var cmd = conn.CreateCommand())
        //            {
        //                cmd.CommandText = @"
        //                    SELECT DISTINCT BlockHash
        //                    FROM BlockTransactions";

        //                using (var reader = cmd.ExecuteReader())
        //                {
        //                    while (reader.Read())
        //                    {
        //                        var blockHash = reader.GetUInt256(0);
        //                        yield return blockHash;
        //                    }
        //                }
        //            }
        //        }

        //        public bool TryReadTransaction(TxKey txKey, out Transaction transaction)
        //        {
        //            using (var conn = this.OpenConnection())
        //            using (var cmd = conn.CreateCommand())
        //            {
        //                cmd.CommandText = @"
        //                    SELECT TxHash, TxBytes
        //                    FROM BlockTransactions
        //                    WHERE BlockHash = @blockHash AND TxIndex = @txIndex";

        //                cmd.Parameters.SetValue("@blockHash", SqlDbType.Binary, 32).Value = txKey.BlockHash.ToDbByteArray();
        //                cmd.Parameters.SetValue("@txIndex", SqlDbType.Int).Value = txKey.TxIndex.ToIntChecked();

        //                using (var reader = cmd.ExecuteReader())
        //                {
        //                    if (reader.Read())
        //                    {
        //                        var txHash = reader.GetUInt256(0);
        //                        var txBytes = reader.GetBytes(1);

        //                        transaction = StorageEncoder.DecodeTransaction(txBytes.ToMemoryStream(), txHash);
        //                        return true;
        //                    }
        //                    else
        //                    {
        //                        transaction = default(Transaction);
        //                        return false;
        //                    }
        //                }
        //            }
        //        }
    }
}
