using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    public class ChainStateStorage : IChainStateStorage
    {
        private readonly UInt256 blockHash;
        private readonly string directory;
        private PersistentByteDictionary unspentTransactions;
        private PersistentByteDictionary unspentOutputs;
        private readonly ReaderWriterLockSlim utxoLock;

        internal ChainStateStorage(UInt256 blockHash)
        {
            this.blockHash = blockHash;
            this.directory = GetDirectory(blockHash);
            this.unspentTransactions = new PersistentByteDictionary(this.directory + "_tx");
            this.unspentOutputs = new PersistentByteDictionary(this.directory + "_output");
            this.utxoLock = new ReaderWriterLockSlim();
        }

        public UInt256 BlockHash
        {
            get { return this.blockHash; }
        }

        public int TransactionCount
        {
            get
            {
                return this.utxoLock.DoRead(() =>
                    this.unspentTransactions.Count);
            }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return this.utxoLock.DoRead(() =>
                this.unspentTransactions.ContainsKey(txHash.ToByteArray()));
        }

        public bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx)
        {
            bool result = false;
            UnspentTx unspentTxLocal = default(UnspentTx);

            this.utxoLock.DoRead(() =>
            {
                byte[] bytes;
                if (this.unspentTransactions.TryGetValue(txHash.ToByteArray(), out bytes))
                {
                    unspentTxLocal = DataEncoder.DecodeUnspentTx(txHash, bytes);
                    result = true;
                }
            });

            unspentTx = unspentTxLocal;
            return result;
        }

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> UnspentTransactions()
        {
            return this.utxoLock.DoRead(() =>
                this.unspentTransactions.Select(keyPair =>
                {
                    var txHash = new UInt256(keyPair.Key);
                    var unspentTx = DataEncoder.DecodeUnspentTx(txHash, keyPair.Value);

                    return new KeyValuePair<UInt256, UnspentTx>(txHash, unspentTx);
                }));
        }

        public int OutputCount
        {
            get
            {
                return this.utxoLock.DoRead(() =>
                    this.unspentOutputs.Count);
            }
        }

        public bool ContainsOutput(TxOutputKey txOutputKey)
        {
            return this.utxoLock.DoRead(() =>
                this.unspentOutputs.ContainsKey(DataEncoder.EncodeTxOutputKey(txOutputKey)));
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            bool result = false;
            TxOutput txOutputLocal = default(TxOutput);

            this.utxoLock.DoRead(() =>
            {
                byte[] bytes;
                if (this.unspentOutputs.TryGetValue(DataEncoder.EncodeTxOutputKey(txOutputKey), out bytes))
                {
                    txOutputLocal = DataEncoder.DecodeTxOutput(bytes);
                    result = true;
                }
            });

            txOutput = txOutputLocal;
            return result;
        }

        public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> UnspentOutputs()
        {
            return this.utxoLock.DoRead(() =>
                this.unspentOutputs.Select(keyPair =>
                {
                    var txOutputKey = DataEncoder.DecodeTxOutputKey(keyPair.Key);
                    var txOutput = DataEncoder.DecodeTxOutput(keyPair.Value);

                    return new KeyValuePair<TxOutputKey, TxOutput>(txOutputKey, txOutput);
                }));
        }

        internal void Duplicate(string destDirectory)
        {
            this.utxoLock.DoWrite(() =>
            {
                this.unspentTransactions.Dispose();
                this.unspentOutputs.Dispose();

                Directory.CreateDirectory(destDirectory + "_tx");
                foreach (var srcFile in Directory.GetFiles(this.directory + "_tx", "*.edb"))
                    File.Copy(srcFile, Path.Combine(destDirectory + "_tx", Path.GetFileName(srcFile)));
                Directory.CreateDirectory(destDirectory + "_output");
                foreach (var srcFile in Directory.GetFiles(this.directory + "_output", "*.edb"))
                    File.Copy(srcFile, Path.Combine(destDirectory + "_output", Path.GetFileName(srcFile)));

                this.unspentTransactions = new PersistentByteDictionary(this.directory + "_tx");
                this.unspentOutputs = new PersistentByteDictionary(this.directory + "_output");
            });
        }

        public void Dispose()
        {
            this.utxoLock.DoWrite(() =>
            {
                this.unspentTransactions.Dispose();
                this.unspentOutputs.Dispose();
            });
        }

        public void DisposeDelete()
        {
            this.Dispose();

            DeleteUtxoDirectory(this.directory);
        }

        static internal string GetDirectory(UInt256 blockHash)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", blockHash.ToString());
        }

        static internal void DeleteUtxoDirectory(string directory)
        {
            var deleteFiles = new List<string>();
            try { deleteFiles.AddRange(Directory.EnumerateFiles(directory + "_tx", "*.*", SearchOption.AllDirectories)); }
            catch (Exception) { }
            try { deleteFiles.AddRange(Directory.EnumerateFiles(directory + "_output", "*.*", SearchOption.AllDirectories)); }
            catch (Exception) { }

            foreach (var file in deleteFiles)
            {
                try { File.Delete(file); }
                catch (Exception) { }
            }

            try { Directory.Delete(directory + "_tx", recursive: true); }
            catch (Exception) { }

            try { Directory.Delete(directory + "_output", recursive: true); }
            catch (Exception) { }
        }
    }
}
