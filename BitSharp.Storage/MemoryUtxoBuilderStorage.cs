using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class MemoryUtxoBuilderStorage : IUtxoBuilderStorage
    {
        private ImmutableDictionary<UInt256, UnspentTx>.Builder unspentTransactions;
        private ImmutableDictionary<TxOutputKey, TxOutput>.Builder unspentOutputs;

        public MemoryUtxoBuilderStorage(IUtxoStorage parentUtxo)
        {
            if (parentUtxo is MemoryUtxoStorage)
            {
                this.unspentTransactions = ((MemoryUtxoStorage)parentUtxo).UnspentTransactions.ToBuilder();
                this.unspentOutputs = ((MemoryUtxoStorage)parentUtxo).UnspentOutputs.ToBuilder();
            }
            else
            {
                this.unspentTransactions = ImmutableDictionary.CreateRange(parentUtxo.UnspentTransactions()).ToBuilder();
                this.unspentOutputs = ImmutableDictionary.CreateRange(parentUtxo.UnspentOutputs()).ToBuilder();
            }
        }

        public ImmutableDictionary<UInt256, UnspentTx>.Builder UnspentTransactionsDictionary { get { return this.unspentTransactions; } }

        public ImmutableDictionary<TxOutputKey, TxOutput>.Builder UnspentOutputsDictionary { get { return this.unspentOutputs; } }

        public int TransactionCount
        {
            get { return this.unspentTransactions.Count; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return this.unspentTransactions.ContainsKey(txHash);
        }

        public bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx)
        {
            return this.unspentTransactions.TryGetValue(txHash, out unspentTx);
        }

        public void AddTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            this.unspentTransactions.Add(txHash, unspentTx);
        }

        public bool RemoveTransaction(UInt256 txHash)
        {
            return this.unspentTransactions.Remove(txHash);
        }

        public void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            this.unspentTransactions[txHash] = unspentTx;
        }

        IEnumerable<KeyValuePair<UInt256, UnspentTx>> IUtxoBuilderStorage.UnspentTransactions()
        {
            return this.unspentTransactions;
        }

        public int OutputCount
        {
            get { return this.unspentOutputs.Count; }
        }

        public bool ContainsOutput(TxOutputKey txOutputKey)
        {
            return this.unspentOutputs.ContainsKey(txOutputKey);
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            return this.unspentOutputs.TryGetValue(txOutputKey, out txOutput);
        }

        public void AddOutput(TxOutputKey txOutputKey, TxOutput txOutput)
        {
            this.unspentOutputs.Add(txOutputKey, txOutput);
        }

        public bool RemoveOutput(TxOutputKey txOutputKey)
        {
            return this.unspentOutputs.Remove(txOutputKey);
        }

        IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> IUtxoBuilderStorage.UnspentOutputs()
        {
            return this.unspentOutputs;
        }

        public void Flush()
        {
        }

        public IUtxoStorage Close(UInt256 blockHash)
        {
            var compactUnspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();
            foreach (var unspentTransaction in this.unspentTransactions)
                compactUnspentTransactions.Add(unspentTransaction);

            var compactUnspentOutputs = ImmutableDictionary.CreateBuilder<TxOutputKey, TxOutput>();
            foreach (var unspentOutput in this.unspentOutputs)
                compactUnspentOutputs.Add(unspentOutput);

            return new MemoryUtxoStorage(blockHash, compactUnspentTransactions.ToImmutable(), compactUnspentOutputs.ToImmutable());
        }

        public void Dispose()
        {
        }
    }
}
