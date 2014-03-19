using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using Microsoft.Isam.Esent.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Esent
{
    public class PersistentUtxoBuilderStorage : IUtxoBuilderStorage
    {
        private readonly string directory;
        private readonly PersistentUInt256ByteDictionary utxo;
        private bool closed = false;

        public PersistentUtxoBuilderStorage(Utxo parentUtxo)
        {
            this.directory = GetDirectory();
            if (parentUtxo is PersistentUtxo)
            {
                ((PersistentUtxo)parentUtxo).Duplicate(this.directory);
                this.utxo = new PersistentUInt256ByteDictionary(this.directory);
            }
            else
            {
                this.utxo = new PersistentUInt256ByteDictionary(this.directory);
                foreach (var unspentTx in parentUtxo.UnspentTransactions())
                {
                    this.Add(unspentTx.TxHash, unspentTx);
                }
            }
        }

        ~PersistentUtxoBuilderStorage()
        {
            this.Dispose();
        }

        public bool ContainsKey(UInt256 txHash)
        {
            return this.utxo.ContainsKey(txHash);
        }

        public bool Remove(UInt256 txHash)
        {
            return this.utxo.Remove(txHash);
        }

        public void Clear()
        {
            this.utxo.Clear();
        }

        public void Add(UInt256 txHash, UnspentTx unspentTx)
        {
            this.utxo.Add(txHash, StorageEncoder.EncodeUnspentTx(unspentTx));
        }

        public int Count { get { return this.utxo.Count; } }

        public UnspentTx this[UInt256 txHash]
        {
            get
            {
                return StorageEncoder.DecodeUnspentTx(txHash, this.utxo[txHash].ToMemoryStream());
            }
            set
            {
                this.utxo[txHash] = StorageEncoder.EncodeUnspentTx(value);
            }
        }

        public void Flush()
        {
            this.utxo.Flush();
        }

        public Utxo Close(UInt256 blockHash)
        {
            this.closed = true;
            this.Dispose();

            //TODO obviously a stop gap here...
            var destPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", PersistentUtxo.GetDirectory(blockHash));
            if (Directory.Exists(destPath))
                Directory.Delete(destPath, recursive: true);
            Directory.CreateDirectory(destPath);

            foreach (var srcFile in Directory.GetFiles(this.directory, "*.edb"))
                File.Move(srcFile, Path.Combine(destPath, Path.GetFileName(srcFile)));
            Directory.Delete(this.directory, recursive: true);

            return new PersistentUtxo(blockHash);
        }

        public void Dispose()
        {
            if (!this.closed)
            {
                this.utxo.Dispose();
                
                try { Directory.Delete(this.directory, recursive: true); }
                catch (IOException) { }
            }
            else
            {
                this.utxo.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        private string GetDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", Guid.NewGuid().ToString());
        }
    }
}
