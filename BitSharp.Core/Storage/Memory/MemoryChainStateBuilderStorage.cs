using BitSharp.Common;
using BitSharp.Core.Builders;
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
    public class MemoryChainStateBuilderStorage : IChainStateBuilderStorage
    {
        private ChainBuilder chain;
        private ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions;
        private ImmutableDictionary<int, List<SpentTx>>.Builder spentTransactions;

        private bool inTransaction;
        private Chain savedChain;
        private ImmutableSortedDictionary<UInt256, UnspentTx> savedUnspentTransactions;
        private ImmutableDictionary<int, List<SpentTx>> savedSpentTransactions;

        public MemoryChainStateBuilderStorage(ChainedHeader genesisHeader)
        {
            this.chain = Chain.CreateForGenesisBlock(genesisHeader).ToBuilder();
            this.unspentTransactions = ImmutableSortedDictionary.CreateBuilder<UInt256, UnspentTx>();
            this.spentTransactions = ImmutableDictionary.CreateBuilder<int, List<SpentTx>>();
        }

        internal ImmutableSortedDictionary<UInt256, UnspentTx>.Builder UnspentTransactionsDictionary { get { return this.unspentTransactions; } }

        public IEnumerable<ChainedHeader> ReadChain()
        {
            return this.chain.Blocks;
        }

        public void AddChainedHeader(ChainedHeader chainedHeader)
        {
            this.chain.AddBlock(chainedHeader);
        }

        public void RemoveChainedHeader(ChainedHeader chainedHeader)
        {
            this.chain.RemoveBlock(chainedHeader);
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

        public bool TryAddTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            try
            {
                this.unspentTransactions.Add(txHash, unspentTx);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public bool TryRemoveTransaction(UInt256 txHash)
        {
            UnspentTx unspentTx;
            if (!this.unspentTransactions.TryGetValue(txHash, out unspentTx))
                return false;

            return this.unspentTransactions.Remove(txHash);
        }

        public void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            this.unspentTransactions[txHash] = unspentTx;
        }

        public IEnumerable<UnspentTx> ReadUnspentTransactions()
        {
            return this.unspentTransactions.Values;
        }

        public void PrepareSpentTransactions(int spentBlockIndex)
        {
            this.spentTransactions.Add(spentBlockIndex, new List<SpentTx>());
        }

        public IEnumerable<SpentTx> ReadSpentTransactions(int spentBlockIndex)
        {
            List<SpentTx> spentTxes;
            if (this.spentTransactions.TryGetValue(spentBlockIndex, out spentTxes))
            {
                foreach (var spentTx in spentTxes)
                    yield return spentTx;
            }
        }

        public void AddSpentTransaction(SpentTx spentTx)
        {
            this.spentTransactions[spentTx.SpentBlockIndex].Add(spentTx);
        }

        public void RemoveSpentTransactions(int spentBlockIndex)
        {
            this.spentTransactions.Remove(spentBlockIndex);
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            this.spentTransactions.RemoveRange(Enumerable.Range(0, spentBlockIndex));
        }

        public IChainStateStorage ToImmutable()
        {
            //TODO figure out if creating clean dictionaries actually has any benefits
            if (true)
            {
                return new MemoryChainStateStorage(this.chain.ToImmutable(), this.unspentTransactions.ToImmutable());
            }
            else
            {
                var compactUnspentTransactions = ImmutableSortedDictionary.CreateBuilder<UInt256, UnspentTx>();
                foreach (var unspentTransaction in this.unspentTransactions)
                    compactUnspentTransactions.Add(unspentTransaction);

                return new MemoryChainStateStorage(this.chain.ToImmutable(), compactUnspentTransactions.ToImmutable());
            }
        }

        public void Dispose()
        {
        }

        public void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            this.savedChain = this.chain.ToImmutable();
            this.savedUnspentTransactions = this.unspentTransactions.ToImmutable();
            this.savedSpentTransactions = this.spentTransactions.ToImmutable();

            this.inTransaction = true;
        }

        public void CommitTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.savedChain = null;
            this.savedUnspentTransactions = null;
            this.savedSpentTransactions = null;

            this.inTransaction = false;
        }

        public void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chain = this.savedChain.ToBuilder();
            this.unspentTransactions = this.savedUnspentTransactions.ToBuilder();
            this.spentTransactions = this.savedSpentTransactions.ToBuilder();

            this.savedChain = null;
            this.savedUnspentTransactions = null;
            this.savedSpentTransactions = null;

            this.inTransaction = false;
        }

        public void Defragment()
        {
        }
    }
}
