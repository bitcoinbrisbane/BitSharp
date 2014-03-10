using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using Microsoft.Isam.Esent.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class PersistentUtxo : Utxo
    {
        private readonly UInt256 blockHash;
        private readonly string directory;
        private PersistentUInt256ByteDictionary utxo;
        private readonly ReaderWriterLockSlim utxoLock;

        static internal string GetDirectory(UInt256 blockHash)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", blockHash.ToString());
        }

        static internal UnspentTx DeserializeUnspentTx(UInt256 key, byte[] value)
        {
            var txHash = key;

            var length = Bits.ToInt32(new ArraySegment<byte>(value, 0, 4).ToArray());
            var unspentOutputsBytes = new ArraySegment<byte>(value, 4, value.Length - 4).ToArray();

            var unspentOutputs = new ImmutableBitArray(unspentOutputsBytes, length);

            return new UnspentTx(txHash, unspentOutputs);
        }

        static internal byte[] SerializeUnspentTx(UnspentTx unspentTx)
        {
            var lengthBytes = Bits.GetBytes(unspentTx.UnspentOutputs.Length);
            var unspentOutputsBytes = unspentTx.UnspentOutputs.ToByteArray();

            var bytes = new byte[lengthBytes.Length + unspentOutputsBytes.Length];
            Buffer.BlockCopy(lengthBytes, 0, bytes, 0, lengthBytes.Length);
            Buffer.BlockCopy(unspentOutputsBytes, 0, bytes, lengthBytes.Length, unspentOutputsBytes.Length);

            return bytes;
        }

        public PersistentUtxo(UInt256 blockHash)
        {
            this.blockHash = blockHash;
            this.directory = GetDirectory(blockHash);
            this.utxo = new PersistentUInt256ByteDictionary(this.directory);
            this.utxoLock = new ReaderWriterLockSlim();
        }

        public UInt256 BlockHash
        {
            get { return this.blockHash; }
        }

        public int Count
        {
            get { return this.utxoLock.DoRead(() => this.utxo.Count); }
        }

        public IEnumerable<UnspentTx> UnspentTransactions()
        {
            this.utxoLock.EnterReadLock();
            try
            {
                foreach (var rawUnspentTx in utxo)
                {
                    yield return DeserializeUnspentTx(rawUnspentTx.Key, rawUnspentTx.Value);
                }
            }
            finally
            {
                this.utxoLock.ExitReadLock();
            }
        }

        public UtxoBuilder ToBuilder()
        {
            return new PersistentUtxoBuilder(this);
        }

        public bool ContainsKey(Common.UInt256 txHash)
        {
            return this.utxoLock.DoRead(() => utxo.ContainsKey(txHash));
        }

        public UnspentTx this[Common.UInt256 txHash]
        {
            get { return this.utxoLock.DoRead(() => DeserializeUnspentTx(txHash, this.utxo[txHash])); }
        }

        internal void Duplicate(string destDirectory)
        {
            this.utxoLock.DoWrite(() =>
            {
                this.utxo.Dispose();

                Directory.CreateDirectory(destDirectory);
                foreach (var srcFile in Directory.GetFiles(this.directory, "*.edb"))
                    File.Copy(srcFile, Path.Combine(destDirectory, Path.GetFileName(srcFile)));

                this.utxo = new PersistentUInt256ByteDictionary(this.directory);
            });
        }

        public void Dispose()
        {
            this.utxo.Dispose();
        }

        public void DisposeDelete()
        {
            this.Dispose();
            Directory.Delete(this.directory, recursive: true);
        }
    }
}
