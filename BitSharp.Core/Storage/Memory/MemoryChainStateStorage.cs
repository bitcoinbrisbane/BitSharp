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
        private int blockHeight;
        private UInt256 blockHash;
        private ImmutableSortedDictionary<UInt256, UnspentTx> unspentTransactions;

        public MemoryChainStateStorage(int blockHeight, UInt256 blockHash, ImmutableSortedDictionary<UInt256, UnspentTx> unspentTransactions)
        {
            this.blockHeight = blockHeight;
            this.blockHash = blockHash;
            this.unspentTransactions = unspentTransactions;
        }

        public ImmutableSortedDictionary<UInt256, UnspentTx> UnspentTransactions { get { return this.unspentTransactions; } }

        public int BlockHeight
        {
            get { return this.blockHeight; }
        }

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

        public void Dispose()
        {
        }
    }
}
