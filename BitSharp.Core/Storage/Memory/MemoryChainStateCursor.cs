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
        private ImmutableDictionary<int, IImmutableList<SpentTx>>.Builder blockSpentTxes;

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

        public void Dispose()
        {
        }

        public bool InTransaction
        {
            get { return this.inTransaction; }
        }

        public void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateStorage.BeginTransaction(out this.chain, out this.unspentTransactions, out this.blockSpentTxes, out this.chainVersion, out this.unspentTxesVersion, out this.spentTxesVersion);

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
                this.spentTxesModified ? this.blockSpentTxes : null,
                this.chainVersion, this.unspentTxesVersion, this.spentTxesVersion);

            this.chain = null;
            this.unspentTransactions = null;
            this.blockSpentTxes = null;

            this.inTransaction = false;
        }

        public void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chain = null;
            this.unspentTransactions = null;
            this.blockSpentTxes = null;

            this.inTransaction = false;
        }

        public IEnumerable<ChainedHeader> ReadChain()
        {
            if (this.inTransaction)
                return this.chain.Blocks;
            else
                return this.chainStateStorage.ReadChain();
        }

        public ChainedHeader GetChainTip()
        {
            if (this.inTransaction)
                return this.chain.LastBlock;
            else
                return this.chainStateStorage.GetChainTip();
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

        public bool ContainsBlockSpentTxes(int blockIndex)
        {
            if (this.inTransaction)
                return this.blockSpentTxes.ContainsKey(blockIndex);
            else
                return this.chainStateStorage.ContainsBlockSpentTxes(blockIndex);
        }

        public bool TryGetBlockSpentTxes(int blockIndex, out IImmutableList<SpentTx> spentTxes)
        {
            if (this.inTransaction)
            {
                return this.blockSpentTxes.TryGetValue(blockIndex, out spentTxes);
            }
            else
            {
                return this.chainStateStorage.TryGetBlockSpentTxes(blockIndex, out spentTxes);
            }
        }

        public bool TryAddBlockSpentTxes(int blockIndex, IImmutableList<SpentTx> spentTxes)
        {
            if (this.inTransaction)
            {
                try
                {
                    this.blockSpentTxes.Add(blockIndex, spentTxes);
                    this.spentTxesModified = true;
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
            else
            {
                return this.chainStateStorage.TryAddBlockSpentTxes(blockIndex, spentTxes);
            }
        }

        public bool TryRemoveBlockSpentTxes(int blockIndex)
        {
            if (this.inTransaction)
            {
                var wasRemoved = this.blockSpentTxes.Remove(blockIndex);
                if (wasRemoved)
                    this.spentTxesModified = true;

                return wasRemoved;
            }
            else
            {
                return this.chainStateStorage.TryRemoveBlockSpentTxes(blockIndex);
            }
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            if (this.inTransaction)
            {
                this.blockSpentTxes.RemoveRange(Enumerable.Range(0, spentBlockIndex));
                this.spentTxesModified = true;
            }
            else
            {
                this.chainStateStorage.RemoveSpentTransactionsToHeight(spentBlockIndex);
            }
        }

        public void Flush()
        {
        }

        public void Defragment()
        {
        }
    }
}
