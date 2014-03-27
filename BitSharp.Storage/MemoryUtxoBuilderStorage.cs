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
        private ImmutableDictionary<UInt256, OutputStates>.Builder unspentTransactions;
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

        public ImmutableDictionary<UInt256, OutputStates>.Builder UnspentTransactionsDictionary { get { return this.unspentTransactions; } }

        public ImmutableDictionary<TxOutputKey, TxOutput>.Builder UnspentOutputsDictionary { get { return this.unspentOutputs; } }

        public int TransactionCount
        {
            get { return this.unspentTransactions.Count; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return this.unspentTransactions.ContainsKey(txHash);
        }

        public bool TryGetTransaction(UInt256 txHash, out OutputStates outputStates)
        {
            return this.unspentTransactions.TryGetValue(txHash, out outputStates);
        }

        public void AddTransaction(UInt256 txHash, OutputStates outputStates)
        {
            this.unspentTransactions.Add(txHash, outputStates);
        }

        public bool RemoveTransaction(UInt256 txHash)
        {
            return this.unspentTransactions.Remove(txHash);
        }

        public void UpdateTransaction(UInt256 txHash, OutputStates outputStates)
        {
            this.unspentTransactions[txHash] = outputStates;
        }

        IEnumerable<KeyValuePair<UInt256, OutputStates>> IUtxoBuilderStorage.UnspentTransactions()
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
            return new MemoryUtxoStorage(blockHash, this.unspentTransactions.ToImmutable(), this.unspentOutputs.ToImmutable());
        }

        public void Dispose()
        {
        }
    }
}
