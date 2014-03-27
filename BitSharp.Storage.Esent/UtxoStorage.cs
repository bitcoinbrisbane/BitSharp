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

namespace BitSharp.Storage.Esent
{
    public class UtxoStorage : IUtxoStorage
    {
        private readonly UInt256 blockHash;
        private readonly string directory;
        private PersistentByteDictionary unspentTransactions;
        private PersistentByteDictionary unspentOutputs;
        private readonly ReaderWriterLockSlim utxoLock;

        static internal string GetDirectory(UInt256 blockHash)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", blockHash.ToString());
        }

        internal UtxoStorage(UInt256 blockHash)
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

        public bool TryGetTransaction(UInt256 txHash, out OutputStates outputStates)
        {
            bool result = false;
            OutputStates outputStatesLocal = default(OutputStates);

            this.utxoLock.DoRead(() =>
            {
                byte[] bytes;
                if (this.unspentTransactions.TryGetValue(txHash.ToByteArray(), out bytes))
                {
                    outputStatesLocal = StorageEncoder.DecodeOutputStates(txHash, bytes);
                    result = true;
                }
            });

            outputStates = outputStatesLocal;
            return result;
        }

        public IEnumerable<KeyValuePair<UInt256, OutputStates>> UnspentTransactions()
        {
            return this.utxoLock.DoRead(() =>
                this.unspentTransactions.Select(keyPair =>
                {
                    var txHash = new UInt256(keyPair.Key);
                    var outputStates = StorageEncoder.DecodeOutputStates(txHash, keyPair.Value);

                    return new KeyValuePair<UInt256, OutputStates>(txHash, outputStates);
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
                this.unspentOutputs.ContainsKey(StorageEncoder.EncodeTxOutputKey(txOutputKey)));
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            bool result = false;
            TxOutput txOutputLocal = default(TxOutput);

            this.utxoLock.DoRead(() =>
            {
                byte[] bytes;
                if (this.unspentOutputs.TryGetValue(StorageEncoder.EncodeTxOutputKey(txOutputKey), out bytes))
                {
                    txOutputLocal = StorageEncoder.DecodeTxOutput(bytes);
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
                    var txOutputKey = StorageEncoder.DecodeTxOutputKey(keyPair.Key);
                    var txOutput = StorageEncoder.DecodeTxOutput(keyPair.Value);

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

            try { Directory.Delete(this.directory + "_tx", recursive: true); }
            catch (IOException) { }
            try { Directory.Delete(this.directory + "_output", recursive: true); }
            catch (IOException) { }
        }
    }
}
