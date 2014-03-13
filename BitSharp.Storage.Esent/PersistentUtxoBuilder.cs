using BitSharp.Common;
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
    public class PersistentUtxoBuilder : UtxoBuilder
    {
        private readonly string directory;
        private readonly PersistentUInt256ByteDictionary utxo;
        private bool disposed = false;

        public PersistentUtxoBuilder(Utxo utxo)
        {
            this.directory = GetDirectory();
            this.utxo = new PersistentUInt256ByteDictionary(this.directory);
            foreach (var unspentTx in utxo.UnspentTransactions())
            {
                this.utxo.Add(unspentTx.TxHash, PersistentUtxo.SerializeUnspentTx(unspentTx));
            }
        }

        public PersistentUtxoBuilder(PersistentUtxo parentUtxo)
        {
            this.directory = GetDirectory();
            parentUtxo.Duplicate(this.directory);
            this.utxo = new PersistentUInt256ByteDictionary(this.directory);
        }

        ~PersistentUtxoBuilder()
        {
            this.Dispose();
        }

        public bool ContainsKey(Common.UInt256 txHash)
        {
            return this.utxo.ContainsKey(txHash);
        }

        public bool Remove(Common.UInt256 txHash)
        {
            return this.utxo.Remove(txHash);
        }

        public void Clear()
        {
            this.utxo.Clear();
        }
        
        public void Add(Common.UInt256 txHash, UnspentTx unspentTx)
        {
            this.utxo.Add(txHash, PersistentUtxo.SerializeUnspentTx(unspentTx));
        }

        public int Count { get { return this.utxo.Count; } }

        public UnspentTx this[Common.UInt256 txHash]
        {
            get
            {
                return PersistentUtxo.DeserializeUnspentTx(txHash, this.utxo[txHash]);
            }
            set
            {
                this.utxo[txHash] = PersistentUtxo.SerializeUnspentTx(value);
            }
        }

        public Utxo Close(UInt256 blockHash)
        {
            this.utxo.Dispose();
            this.disposed = true;

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
            if (!this.disposed)
            {
                this.utxo.Dispose();
                this.disposed = true;
                GC.SuppressFinalize(this);
                
                Directory.Delete(this.directory, recursive: true);
            }
        }

        private string GetDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", Guid.NewGuid().ToString());
        }
    }
}
