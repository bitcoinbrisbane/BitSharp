using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryChainStateStorage : IChainStateStorage
    {
        private Chain chain;
        private ImmutableSortedDictionary<UInt256, UnspentTx> unspentTransactions;

        public MemoryChainStateStorage(Chain chain, ImmutableSortedDictionary<UInt256, UnspentTx> unspentTransactions)
        {
            this.chain = chain;
            this.unspentTransactions = unspentTransactions;
        }

        public void Dispose()
        {
        }

        public ImmutableSortedDictionary<UInt256, UnspentTx> UnspentTransactions { get { return this.unspentTransactions; } }

        public Chain Chain
        {
            get { return this.chain; }
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

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> ReadUnspentTransactions()
        {
            return this.unspentTransactions;
        }
    }
}
