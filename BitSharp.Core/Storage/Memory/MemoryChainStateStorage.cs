using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryChainStateStorage : IChainStateStorage
    {
        private UInt256 blockHash;
        private ImmutableDictionary<UInt256, UnspentTx> unspentTransactions;
        private ImmutableDictionary<TxOutputKey, TxOutput> unspentOutputs;

        public MemoryChainStateStorage(UInt256 blockHash, ImmutableDictionary<UInt256, UnspentTx> unspentTransactions, ImmutableDictionary<TxOutputKey, TxOutput> unspentOutputs)
        {
            this.blockHash = blockHash;
            this.unspentTransactions = unspentTransactions;
            this.unspentOutputs = unspentOutputs;
        }

        public ImmutableDictionary<UInt256, UnspentTx> UnspentTransactions { get { return this.unspentTransactions; } }

        public ImmutableDictionary<TxOutputKey, TxOutput> UnspentOutputs { get { return this.unspentOutputs; } }

        public UInt256 BlockHash
        {
            get { return this.blockHash; }
        }

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

        IEnumerable<KeyValuePair<UInt256, UnspentTx>> IChainStateStorage.UnspentTransactions()
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

        IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> IChainStateStorage.UnspentOutputs()
        {
            return this.unspentOutputs;
        }

        public void Dispose()
        {
        }
    }
}
