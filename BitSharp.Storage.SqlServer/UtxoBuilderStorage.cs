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
    public class UtxoBuilderStorage : SqlBase, UtxoBuilder
    {
        private readonly Guid id;
        private readonly SqlConnection conn;
        private bool closed = false;

        private readonly SqlCommand containsKeyCommand;
        private readonly SqlCommand removeCommand;
        private readonly SqlCommand clearCommand;
        private readonly SqlCommand addCommand;
        private readonly SqlCommand countCommand;
        private readonly SqlCommand selectCommand;
        private readonly SqlCommand updateCommand;

        public UtxoBuilderStorage(Utxo parentUtxo)
        {
            this.id = Guid.NewGuid();
            if (parentUtxo is UtxoStorage)
            {
                ((UtxoStorage)parentUtxo).Duplicate(this.id);
            }
            else
            {
                foreach (var unspentTx in parentUtxo.UnspentTransactions())
                {
                    this.Add(unspentTx.TxHash, unspentTx);
                }
            }

            this.conn = this.OpenConnection();

            this.containsKeyCommand = this.conn.CreateCommand();
            this.containsKeyCommand.CommandText = @"
                    SELECT CASE WHEN EXISTS(
                        SELECT * FROM Utxo
                        WHERE Id = @id AND TxHash = @txHash
                    ) THEN 1 ELSE 0 END";
            this.containsKeyCommand.Parameters.Add(new SqlParameter { ParameterName = "@id", SqlDbType = SqlDbType.UniqueIdentifier });
            this.containsKeyCommand.Parameters.Add(new SqlParameter { ParameterName = "@txHash", SqlDbType = SqlDbType.Binary, Size = 32 });
            this.containsKeyCommand.Prepare();

            this.removeCommand = conn.CreateCommand();
            this.removeCommand.CommandText = @"
                    DELETE FROM Utxo
                    WHERE Id = @id AND TxHash = @txHash";
            this.removeCommand.Parameters.Add(new SqlParameter { ParameterName = "@id", SqlDbType = SqlDbType.UniqueIdentifier });
            this.removeCommand.Parameters.Add(new SqlParameter { ParameterName = "@txHash", SqlDbType = SqlDbType.Binary, Size = 32 });
            this.removeCommand.Prepare();

            this.clearCommand = conn.CreateCommand();
            this.clearCommand.CommandText = @"
                    DELETE FROM Utxo
                    WHERE Id = @id";
            this.clearCommand.Parameters.Add(new SqlParameter { ParameterName = "@id", SqlDbType = SqlDbType.UniqueIdentifier });
            this.clearCommand.Prepare();

            this.addCommand = conn.CreateCommand();
            this.addCommand.CommandText = @"
                INSERT INTO Utxo (Id, TxHash, UnspentOutputs)
                VALUES (@id, @txHash, @unspentOutputs)";
            this.addCommand.Parameters.Add(new SqlParameter { ParameterName = "@id", SqlDbType = SqlDbType.UniqueIdentifier });
            this.addCommand.Parameters.Add(new SqlParameter { ParameterName = "@txHash", SqlDbType = SqlDbType.Binary, Size = 32 });
            this.addCommand.Parameters.Add(new SqlParameter { ParameterName = "@unspentOutputs", SqlDbType = SqlDbType.Binary, Size = 100 });
            this.addCommand.Prepare();

            this.countCommand = conn.CreateCommand();
            this.countCommand.CommandText = @"
                SELECT COUNT(*)
                FROM Utxo";
            this.countCommand.Prepare();

            this.selectCommand = conn.CreateCommand();
            this.selectCommand.CommandText = @"
                SELECT UnspentOutputs FROM Utxo
                WHERE Id = @id AND TxHash = @txHash";
            this.selectCommand.Parameters.Add(new SqlParameter { ParameterName = "@id", SqlDbType = SqlDbType.UniqueIdentifier });
            this.selectCommand.Parameters.Add(new SqlParameter { ParameterName = "@txHash", SqlDbType = SqlDbType.Binary, Size = 32 });
            this.selectCommand.Prepare();

            this.updateCommand = conn.CreateCommand();
            this.updateCommand.CommandText = @"
                MERGE Utxo AS target
                USING (SELECT @id, @txHash) AS source (Id, TxHash)
                ON (target.Id = source.Id AND target.TxHash = source.TxHash)
	            WHEN NOT MATCHED THEN	
	                INSERT (Id, TxHash, UnspentOutputs)
                    VALUES (@id, @txHash, @unspentOutputs)
                WHEN MATCHED THEN 
                    UPDATE SET UnspentOutputs = @unspentOutputs;";
            this.updateCommand.Parameters.Add(new SqlParameter { ParameterName = "@id", SqlDbType = SqlDbType.UniqueIdentifier });
            this.updateCommand.Parameters.Add(new SqlParameter { ParameterName = "@txHash", SqlDbType = SqlDbType.Binary, Size = 32 });
            this.updateCommand.Parameters.Add(new SqlParameter { ParameterName = "@unspentOutputs", SqlDbType = SqlDbType.Binary, Size = 32 });
            this.updateCommand.Prepare();
        }

        ~UtxoBuilderStorage()
        {
            this.Dispose();
        }

        public bool ContainsKey(UInt256 txHash)
        {
            this.containsKeyCommand.Parameters["@id"].Value = this.id;
            this.containsKeyCommand.Parameters["@txHash"].Value = txHash.ToDbByteArray();

            return (int)this.containsKeyCommand.ExecuteScalar() == 1;
        }

        public bool Remove(UInt256 txHash)
        {
            this.removeCommand.Parameters["@id"].Value = this.id;
            this.removeCommand.Parameters["@txHash"].Value = txHash.ToDbByteArray();

            return this.removeCommand.ExecuteNonQuery() > 0;
        }

        public void Clear()
        {
            this.clearCommand.Parameters["@id"].Value = this.id;

            this.clearCommand.ExecuteNonQuery();
        }

        public void Add(UInt256 txHash, UnspentTx unspentTx)
        {
            this.addCommand.Parameters["@id"].Value = this.id;
            this.addCommand.Parameters["@txHash"].Value = txHash.ToDbByteArray();
            this.addCommand.Parameters["@unspentOutputs"].Value = StorageEncoder.EncodeUnspentTx(unspentTx);

            this.addCommand.ExecuteNonQuery();
        }

        public int Count
        {
            get
            {
                return (int)this.countCommand.ExecuteScalar();
            }
        }

        public UnspentTx this[UInt256 txHash]
        {
            get
            {
                this.selectCommand.Parameters["@id"].Value = this.id;
                this.selectCommand.Parameters["@txHash"].Value = txHash.ToDbByteArray();

                using (var reader = this.selectCommand.ExecuteReader())
                {
                    if (reader.Read())
                        return StorageEncoder.DecodeUnspentTx(txHash, reader.GetBytes(0).ToMemoryStream());
                    else
                        return default(UnspentTx);
                }
            }
            set
            {
                this.updateCommand.Parameters["@id"].Value = this.id;
                this.updateCommand.Parameters["@txHash"].Value = txHash.ToDbByteArray();
                this.updateCommand.Parameters["@unspentOutputs"].Value = StorageEncoder.EncodeUnspentTx(value);

                this.updateCommand.ExecuteNonQuery();
            }
        }

        public Utxo Close(UInt256 blockHash)
        {
            this.closed = true;
            this.Dispose();

            return new UtxoStorage(this.id, blockHash);
        }

        public void Dispose()
        {
            if (!this.closed)
            {
                this.Clear();
            }

            this.containsKeyCommand.Dispose();
            this.removeCommand.Dispose();
            this.clearCommand.Dispose();
            this.addCommand.Dispose();
            this.countCommand.Dispose();
            this.selectCommand.Dispose();
            this.updateCommand.Dispose();
            this.conn.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
