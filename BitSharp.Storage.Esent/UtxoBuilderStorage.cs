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
    public class UtxoBuilderStorage : IUtxoBuilderStorage
    {
        private readonly string directory;
        private readonly PersistentByteDictionary unspentTransactions;
        private readonly PersistentByteDictionary unspentOutputs;
        private bool closed = false;

        public UtxoBuilderStorage(IUtxoStorage parentUtxo)
        {
            this.directory = GetDirectory();
            if (parentUtxo is UtxoStorage)
            {
                ((UtxoStorage)parentUtxo).Duplicate(this.directory);
                this.unspentTransactions = new PersistentByteDictionary(this.directory + "_tx");
                this.unspentOutputs = new PersistentByteDictionary(this.directory + "_output");
            }
            else
            {
                this.unspentTransactions = new PersistentByteDictionary(this.directory + "_tx");
                this.unspentOutputs = new PersistentByteDictionary(this.directory + "_output");

                foreach (var unspentTx in parentUtxo.UnspentTransactions())
                    this.AddTransaction(unspentTx.Key, unspentTx.Value);

                foreach (var unspentOutput in parentUtxo.UnspentOutputs())
                    this.AddOutput(unspentOutput.Key, unspentOutput.Value);
            }
        }

        ~UtxoBuilderStorage()
        {
            this.Dispose();
        }

        public int TransactionCount
        {
            get { return this.unspentTransactions.Count; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return this.unspentTransactions.ContainsKey(txHash.ToByteArray());
        }

        public bool TryGetTransaction(UInt256 txHash, out OutputStates outputStates)
        {
            byte[] bytes;
            if (this.unspentTransactions.TryGetValue(txHash.ToByteArray(), out bytes))
            {
                outputStates = StorageEncoder.DecodeOutputStates(txHash, bytes);
                return true;
            }
            else
            {
                outputStates = default(OutputStates);
                return false;
            }
        }

        public void AddTransaction(UInt256 txHash, OutputStates outputStates)
        {
            this.unspentTransactions.Add(txHash.ToByteArray(), StorageEncoder.EncodeOutputStates(outputStates));
        }

        public bool RemoveTransaction(UInt256 txHash)
        {
            return this.unspentTransactions.Remove(txHash.ToByteArray());
        }

        public void UpdateTransaction(UInt256 txHash, OutputStates outputStates)
        {
            this.unspentTransactions[txHash.ToByteArray()] = StorageEncoder.EncodeOutputStates(outputStates);
        }

        public IEnumerable<KeyValuePair<UInt256, OutputStates>> UnspentTransactions()
        {
            return this.unspentTransactions.Select(keyPair =>
            {
                var txHash = new UInt256(keyPair.Key);
                var outputStates = StorageEncoder.DecodeOutputStates(txHash, keyPair.Value);

                return new KeyValuePair<UInt256, OutputStates>(txHash, outputStates);
            });
        }

        public int OutputCount
        {
            get { return this.unspentOutputs.Count; }
        }

        public bool ContainsOutput(TxOutputKey txOutputKey)
        {
            return this.unspentOutputs.ContainsKey(StorageEncoder.EncodeTxOutputKey(txOutputKey));
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            byte[] bytes;
            if (this.unspentOutputs.TryGetValue(StorageEncoder.EncodeTxOutputKey(txOutputKey), out bytes))
            {
                txOutput = StorageEncoder.DecodeTxOutput(bytes);
                return true;
            }
            else
            {
                txOutput = default(TxOutput);
                return false;
            }
        }

        public void AddOutput(TxOutputKey txOutputKey, TxOutput txOutput)
        {
            this.unspentOutputs.Add(StorageEncoder.EncodeTxOutputKey(txOutputKey), StorageEncoder.EncodeTxOutput(txOutput));
        }

        public bool RemoveOutput(TxOutputKey txOutputKey)
        {
            return this.unspentOutputs.Remove(StorageEncoder.EncodeTxOutputKey(txOutputKey));
        }

        public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> UnspentOutputs()
        {
            return this.unspentOutputs.Select(keyPair =>
            {
                var txOutputKey = StorageEncoder.DecodeTxOutputKey(keyPair.Key);
                var txOutput = StorageEncoder.DecodeTxOutput(keyPair.Value);

                return new KeyValuePair<TxOutputKey, TxOutput>(txOutputKey, txOutput);
            });
        }

        public void Flush()
        {
            this.unspentTransactions.Flush();
            this.unspentOutputs.Flush();
        }

        public IUtxoStorage Close(UInt256 blockHash)
        {
            this.closed = true;
            this.Dispose();

            //TODO obviously a stop gap here...
            var destPath = UtxoStorage.GetDirectory(blockHash);
            if (Directory.Exists(destPath + "_tx"))
                Directory.Delete(destPath + "_tx", recursive: true);
            Directory.CreateDirectory(destPath + "_tx");
            if (Directory.Exists(destPath + "_output"))
                Directory.Delete(destPath + "_output", recursive: true);
            Directory.CreateDirectory(destPath + "_output");

            foreach (var srcFile in Directory.GetFiles(this.directory + "_tx", "*.edb"))
                File.Move(srcFile, Path.Combine(destPath + "_tx", Path.GetFileName(srcFile)));
            Directory.Delete(this.directory + "_tx", recursive: true);
            foreach (var srcFile in Directory.GetFiles(this.directory + "_output", "*.edb"))
                File.Move(srcFile, Path.Combine(destPath + "_output", Path.GetFileName(srcFile)));
            Directory.Delete(this.directory + "_output", recursive: true);

            return new UtxoStorage(blockHash);
        }

        public void Dispose()
        {
            if (!this.closed)
            {
                this.unspentTransactions.Dispose();
                this.unspentOutputs.Dispose();

                try { Directory.Delete(this.directory + "_tx", recursive: true); }
                catch (IOException) { }
                try { Directory.Delete(this.directory + "_output", recursive: true); }
                catch (IOException) { }
            }
            else
            {
                this.unspentTransactions.Dispose();
                this.unspentOutputs.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        private string GetDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", Guid.NewGuid().ToString());
        }
    }
}
