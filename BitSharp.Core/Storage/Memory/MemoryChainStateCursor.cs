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
    public class MemoryChainStateCursor : IChainStateCursor
    {
        private readonly MemoryChainStateStorage chainStateStorage;

        private bool inTransaction;

        private ChainBuilder chain;
        private ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions;
        private ImmutableDictionary<int, List<SpentTx>>.Builder spentTransactions;

        private long chainVersion;
        private long unspentTxesVersion;
        private long spentTxesVersion;

        private bool chainModified;
        private bool unspentTxesModified;
        private bool spentTxesModified;

        internal MemoryChainStateCursor(MemoryChainStateStorage chainStateStorage)
        {
            this.chainStateStorage = chainStateStorage;
        }

        internal ImmutableSortedDictionary<UInt256, UnspentTx>.Builder UnspentTransactionsDictionary { get { return this.unspentTransactions; } }

        public IEnumerable<ChainedHeader> ReadChain()
        {
            if (this.inTransaction)
                return this.chain.Blocks;
            else
                return this.chainStateStorage.ReadChain();
        }

        public void AddChainedHeader(ChainedHeader chainedHeader)
        {
            if (this.inTransaction)
            {
                this.chain.AddBlock(chainedHeader);
                this.chainModified = true;
            }
            else
            {
                this.chainStateStorage.AddChainedHeader(chainedHeader);
            }
        }

        public void RemoveChainedHeader(ChainedHeader chainedHeader)
        {
            if (this.inTransaction)
            {
                this.chain.RemoveBlock(chainedHeader);
                this.chainModified = true;
            }
            else
            {
                this.chainStateStorage.RemoveChainedHeader(chainedHeader);
            }
        }

        public int UnspentTxCount
        {
            get
            {
                if (this.inTransaction)
                    return this.unspentTransactions.Count;
                else
                    return this.chainStateStorage.UnspentTxCount;
            }
        }

        public bool ContainsUnspentTx(UInt256 txHash)
        {
            if (this.inTransaction)
                return this.unspentTransactions.ContainsKey(txHash);
            else
                return this.chainStateStorage.ContainsUnspentTx(txHash);
        }

        public bool TryGetUnspentTx(UInt256 txHash, out UnspentTx unspentTx)
        {
            if (this.inTransaction)
                return this.unspentTransactions.TryGetValue(txHash, out unspentTx);
            else
                return this.chainStateStorage.TryGetUnspentTx(txHash, out unspentTx);
        }

        public bool TryAddUnspentTx(UnspentTx unspentTx)
        {
            if (this.inTransaction)
            {
                try
                {
                    this.unspentTransactions.Add(unspentTx.TxHash, unspentTx);
                    this.unspentTxesModified = true;
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
            else
            {
                return this.chainStateStorage.TryAddUnspentTx(unspentTx);
            }
        }

        public bool TryRemoveUnspentTx(UInt256 txHash)
        {
            if (this.inTransaction)
            {
                var wasRemoved = this.unspentTransactions.Remove(txHash);
                if (wasRemoved)
                    this.unspentTxesModified = true;

                return wasRemoved;
            }
            else
            {
                return this.chainStateStorage.TryRemoveUnspentTx(txHash);
            }
        }

        public bool TryUpdateUnspentTx(UnspentTx unspentTx)
        {
            if (this.inTransaction)
            {
                if (this.unspentTransactions.ContainsKey(unspentTx.TxHash))
                {
                    this.unspentTransactions[unspentTx.TxHash] = unspentTx;
                    this.unspentTxesModified = true;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return this.chainStateStorage.TryUpdateUnspentTx(unspentTx);
            }
        }

        public IEnumerable<UnspentTx> ReadUnspentTransactions()
        {
            if (this.inTransaction)
                return this.unspentTransactions.Values;
            else
                return this.chainStateStorage.ReadUnspentTransactions();
        }

        public void PrepareSpentTransactions(int spentBlockIndex)
        {
            if (this.inTransaction)
            {
                this.spentTransactions.Add(spentBlockIndex, new List<SpentTx>());
                this.spentTxesModified = true;
            }
            else
            {
                this.chainStateStorage.PrepareSpentTransactions(spentBlockIndex);
            }
        }

        public IEnumerable<SpentTx> ReadSpentTransactions(int spentBlockIndex)
        {
            if (this.inTransaction)
            {
                List<SpentTx> spentTxes;
                if (this.spentTransactions.TryGetValue(spentBlockIndex, out spentTxes))
                {
                    foreach (var spentTx in spentTxes)
                        yield return spentTx;
                }
            }
            else
            {
                foreach (var spentTx in this.chainStateStorage.ReadSpentTransactions(spentBlockIndex))
                    yield return spentTx;
            }
        }

        public void AddSpentTransaction(SpentTx spentTx)
        {
            if (this.inTransaction)
            {
                this.spentTransactions[spentTx.SpentBlockIndex].Add(spentTx);
                this.spentTxesModified = true;
            }
            else
            {
                this.chainStateStorage.AddSpentTransaction(spentTx);
            }
        }

        public void RemoveSpentTransactions(int spentBlockIndex)
        {
            if (this.inTransaction)
            {
                this.spentTransactions.Remove(spentBlockIndex);
                this.spentTxesModified = true;
            }
            else
            {
                this.chainStateStorage.RemoveSpentTransactions(spentBlockIndex);
            }
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            if (this.inTransaction)
            {
                this.spentTransactions.RemoveRange(Enumerable.Range(0, spentBlockIndex));
                this.spentTxesModified = true;
            }
            else
            {
                this.chainStateStorage.RemoveSpentTransactionsToHeight(spentBlockIndex);
            }
        }

        public void Dispose()
        {
        }

        public void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateStorage.BeginTransaction(out this.chain, out this.unspentTransactions, out this.spentTransactions, out this.chainVersion, out this.unspentTxesVersion, out this.spentTxesVersion);

            this.chainModified = false;
            this.unspentTxesModified = false;
            this.spentTxesModified = false;

            this.inTransaction = true;
        }

        public void CommitTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateStorage.CommitTransaction(
                this.chainModified ? this.chain : null,
                this.unspentTxesModified ? this.unspentTransactions : null,
                this.spentTxesModified ? this.spentTransactions : null,
                this.chainVersion, this.unspentTxesVersion, this.spentTxesVersion);

            this.chain = null;
            this.unspentTransactions = null;
            this.spentTransactions = null;

            this.inTransaction = false;
        }

        public void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chain = null;
            this.unspentTransactions = null;
            this.spentTransactions = null;

            this.inTransaction = false;
        }

        public void Defragment()
        {
        }
    }
}
