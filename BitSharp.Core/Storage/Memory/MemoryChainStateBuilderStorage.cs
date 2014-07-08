using BitSharp.Common;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryChainStateBuilderStorage : IChainStateBuilderStorage
    {
        private ChainBuilder chain;
        private ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions;

        private Chain savedChain;
        private ImmutableSortedDictionary<UInt256, UnspentTx> savedUnspentTransactions;

        public MemoryChainStateBuilderStorage(IChainStateStorage parentChainState)
        {
            this.chain = parentChainState.Chain.ToBuilder();
            if (parentChainState is MemoryChainStateStorage)
            {
                this.unspentTransactions = ((MemoryChainStateStorage)parentChainState).UnspentTransactions.ToBuilder();
            }
            else
            {
                this.unspentTransactions = ImmutableSortedDictionary.CreateRange(parentChainState.ReadUnspentTransactions()).ToBuilder();
            }
        }

        public MemoryChainStateBuilderStorage(ChainedHeader genesisHeader)
        {
            this.chain = Chain.CreateForGenesisBlock(genesisHeader).ToBuilder();
            this.unspentTransactions = ImmutableSortedDictionary.CreateBuilder<UInt256, UnspentTx>();
        }

        public ImmutableSortedDictionary<UInt256, UnspentTx>.Builder UnspentTransactionsDictionary { get { return this.unspentTransactions; } }

        public Chain Chain
        {
            get { return this.chain.ToImmutable(); }
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

        public bool TryGetTransaction(UInt256 txHash, int spentBlockIndex, out UnspentTx unspentTx)
        {
            throw new NotImplementedException();
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

        public bool RemoveTransaction(UInt256 txHash, int spentBlockIndex)
        {
            return this.unspentTransactions.Remove(txHash);
        }

        public void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            this.unspentTransactions[txHash] = unspentTx;
        }

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> ReadUnspentTransactions()
        {
            return this.unspentTransactions;
        }

        public void RemoveSpentTransactions(int spentBlockIndex)
        {
            throw new NotImplementedException();
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            throw new NotImplementedException();
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
            if (this.savedChain != null || this.savedUnspentTransactions != null)
                throw new InvalidOperationException();

            //if (this.blockHeight % 10000 == 0)
            //{
            //    var compactUnspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();
            //    compactUnspentTransactions.AddRange(this.unspentTransactions);

            //    this.unspentTransactions = compactUnspentTransactions;
            //}

            this.savedChain = this.chain.ToImmutable();
            this.savedUnspentTransactions = this.unspentTransactions.ToImmutable();
        }

        public void CommitTransaction()
        {
            if (this.savedChain == null || this.savedUnspentTransactions == null)
                throw new InvalidOperationException();

            this.savedChain = null;
            this.savedUnspentTransactions = null;
        }

        public void RollbackTransaction()
        {
            if (this.savedChain == null || this.savedUnspentTransactions == null)
                throw new InvalidOperationException();

            this.chain = this.savedChain.ToBuilder();
            this.unspentTransactions = this.savedUnspentTransactions.ToBuilder();

            this.savedChain = null;
            this.savedUnspentTransactions = null;
        }
    }
}
