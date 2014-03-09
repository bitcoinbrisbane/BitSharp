using BitSharp.Common;
using BitSharp.Data;
using Microsoft.Isam.Esent.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class PersistentUtxo : Utxo
    {
        private UInt256 _blockHash;
        private PersistentUInt256ByteDictionary _utxo;

        static internal string FolderPath(UInt256 blockHash)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", blockHash.ToString());
        }

        static internal UnspentTx DeserializeUnspentTx(UInt256 key, byte[] value)
        {
            var txHash = key;
            var unspentOutputs = new ImmutableBitArray(value);

            return new UnspentTx(txHash, unspentOutputs);
        }

        static internal byte[] SerializeUnspentTx(UnspentTx unspentTx)
        {
            return unspentTx.UnspentOutputs.ToByteArray();
        }

        public PersistentUtxo(UInt256 blockHash)
        {
            this._blockHash = blockHash;
            this._utxo = new PersistentUInt256ByteDictionary(FolderPath(blockHash));
        }

        internal PersistentUtxo(UInt256 blockHash, PersistentUInt256ByteDictionary utxo)
        {
            this._blockHash = blockHash;
            this._utxo = utxo;
        }

        public UInt256 BlockHash
        {
            get { return this._blockHash; }
        }

        public int Count
        {
            get { return this._utxo.Count; }
        }

        public IEnumerable<UnspentTx> UnspentTransactions()
        {
            foreach (var rawUnspentTx in _utxo)
            {
                yield return DeserializeUnspentTx(rawUnspentTx.Key, rawUnspentTx.Value);
            }
        }

        public UtxoBuilder ToBuilder(UInt256 blockHash)
        {
            return new PersistentUtxoBuilder(blockHash, this);
        }

        public bool ContainsKey(Common.UInt256 txHash)
        {
            return _utxo.ContainsKey(txHash);
        }

        public UnspentTx this[Common.UInt256 txHash]
        {
            get { return DeserializeUnspentTx(txHash, this._utxo[txHash]); }
        }

        internal void Duplicate(UInt256 blockHash)
        {
            if (blockHash == this.BlockHash)
                throw new InvalidOperationException();

            this._utxo.Dispose();
            var srcPath = FolderPath(this.BlockHash);
            var destPath = FolderPath(blockHash);

            if (Directory.Exists(destPath))
                Directory.Delete(destPath, recursive: true);

            Directory.CreateDirectory(destPath);
            foreach (var srcFile in Directory.GetFiles(srcPath, "*.edb"))
                File.Copy(srcFile, Path.Combine(destPath, Path.GetFileName(srcFile)));

            this._utxo = new PersistentUInt256ByteDictionary(srcPath);
        }

        public void Dispose()
        {
            this._utxo.Dispose();
        }

        public void DisposeDelete()
        {
            Dispose();
            Directory.Delete(FolderPath(this.BlockHash), recursive: true);
        }
    }
}
