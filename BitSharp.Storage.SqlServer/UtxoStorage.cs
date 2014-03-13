using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage.SqlServer.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.SqlServer
{
    public class UtxoStorage : SqlBase, Utxo
    {
        private readonly Guid id;
        private readonly UInt256 blockHash;

        internal UtxoStorage(Guid id, UInt256 blockHash)
        {
            this.id = id;
            this.blockHash = blockHash;
        }

        internal Guid Id
        {
            get { return this.id; }
        }

        public UInt256 BlockHash
        {
            get { return this.blockHash; }
        }

        public int Count
        {
            get
            {
                using (var conn = this.OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT COUNT(*)
                        FROM Utxo";

                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        public IEnumerable<UnspentTx> UnspentTransactions()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TxHash, UnspentTxOutputs
                    FROM Utxo
                    WHERE Id = @id";

                cmd.Parameters.SetValue("@id", SqlDbType.UniqueIdentifier).Value = this.id;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var txHash = reader.GetUInt256(0);
                        var unspentTxOutputsBytes = reader.GetBytes(1);

                        yield return StorageEncoder.DecodeUnspentTx(txHash, unspentTxOutputsBytes.ToMemoryStream());
                    }
                }
            }
        }

        public UtxoBuilder ToBuilder()
        {
            return new UtxoBuilderStorage(this);
        }

        public bool ContainsKey(UInt256 txHash)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT CASE WHEN EXISTS(
                        SELECT * FROM Utxo
                        WHERE Id = @id AND TxHash = @txHash
                    ) THEN 1 ELSE 0 END";

                cmd.Parameters.SetValue("@id", SqlDbType.UniqueIdentifier).Value = this.id;
                cmd.Parameters.SetValue("@txHash", SqlDbType.Binary, 32).Value = txHash.ToDbByteArray();

                return (int)cmd.ExecuteScalar() == 1;
            }
        }

        public UnspentTx this[UInt256 txHash]
        {
            get
            {
                using (var conn = this.OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT UnspentOutputs FROM Utxo
                        WHERE Id = @id AND TxHash = @txHash";

                    cmd.Parameters.SetValue("@id", SqlDbType.UniqueIdentifier).Value = this.id;
                    cmd.Parameters.SetValue("@txHash", SqlDbType.Binary, 32).Value = txHash.ToDbByteArray();

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            return StorageEncoder.DecodeUnspentTx(txHash, reader.GetBytes(0).ToMemoryStream());
                        else
                            return default(UnspentTx);
                    }
                }
            }
        }

        public void Dispose()
        {
        }

        public void DisposeDelete()
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    DELETE FROM Utxo
                    WHERE Id = @id";

                cmd.Parameters.SetValue("@id", SqlDbType.UniqueIdentifier).Value = this.id;

                cmd.ExecuteNonQuery();
            }
        }

        internal void Duplicate(Guid destId)
        {
            using (var conn = this.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO Utxo (Id, TxHash, UnspentOutputs)
                    SELECT @destId, TxHash, UnspentOutputs
                    FROM UTXO
                    WHERE Id = @srcId";

                cmd.Parameters.SetValue("@srcId", SqlDbType.UniqueIdentifier).Value = this.id;
                cmd.Parameters.SetValue("@destId", SqlDbType.UniqueIdentifier).Value = destId;

                cmd.ExecuteNonQuery();
            }
        }
    }
}
