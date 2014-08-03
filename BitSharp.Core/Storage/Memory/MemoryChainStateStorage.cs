using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    internal class MemoryChainStateStorage
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);

        private ChainBuilder chain;
        private ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions;
        private ImmutableDictionary<int, List<SpentTx>>.Builder spentTransactions;

        private long chainVersion;
        private long unspentTxesVersion;
        private long spentTxesVersion;

        public MemoryChainStateStorage(Chain chain = null, ImmutableSortedDictionary<UInt256, UnspentTx> unspentTransactions = null, ImmutableDictionary<int, List<SpentTx>> spentTransactions = null)
        {
            this.chain = chain != null ? chain.ToBuilder() : new ChainBuilder();
            this.unspentTransactions = unspentTransactions != null ? unspentTransactions.ToBuilder() : ImmutableSortedDictionary.CreateBuilder<UInt256, UnspentTx>();
            this.spentTransactions = spentTransactions != null ? spentTransactions.ToBuilder() : ImmutableDictionary.CreateBuilder<int, List<SpentTx>>();
        }

        public void Dispose()
        {
        }

        public void BeginTransaction(out ChainBuilder chain, out ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions, out ImmutableDictionary<int, List<SpentTx>>.Builder spentTransactions, out long chainVersion, out long unspentTxesVersion, out long spentTxesVersion)
        {
            this.semaphore.Wait();
            try
            {
                chain = this.chain.ToImmutable().ToBuilder();
                unspentTransactions = this.unspentTransactions.ToImmutable().ToBuilder();
                spentTransactions = this.spentTransactions.ToImmutable().ToBuilder();

                chainVersion = this.chainVersion;
                unspentTxesVersion = this.unspentTxesVersion;
                spentTxesVersion = this.spentTxesVersion;
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public void CommitTransaction(ChainBuilder chain, ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions, ImmutableDictionary<int, List<SpentTx>>.Builder spentTransactions, long chainVersion, long unspentTxesVersion, long spentTxesVersion)
        {
            this.semaphore.Do(() =>
            {
                if (chain != null && this.chainVersion != chainVersion
                    || unspentTransactions != null && unspentTxesVersion != this.unspentTxesVersion
                    || spentTransactions != null && spentTxesVersion != this.spentTxesVersion)
                    throw new InvalidOperationException();

                if (chain != null)
                {
                    this.chain = chain.ToImmutable().ToBuilder();
                    this.chainVersion++;
                }

                if (unspentTransactions != null)
                {
                    this.unspentTransactions = unspentTransactions.ToImmutable().ToBuilder();
                    this.unspentTxesVersion++;
                }

                if (spentTransactions != null)
                {
                    this.spentTransactions = spentTransactions.ToImmutable().ToBuilder();
                    this.spentTxesVersion++;
                }
            });
        }

        public IEnumerable<ChainedHeader> ReadChain()
        {
            return this.semaphore.Do(() =>
                this.chain.ToImmutable()).Blocks;
        }

        public void AddChainedHeader(ChainedHeader chainedHeader)
        {
            this.semaphore.Do(() =>
            {
                this.chain.AddBlock(chainedHeader);
                this.chainVersion++;
            });
        }

        public void RemoveChainedHeader(ChainedHeader chainedHeader)
        {
            this.semaphore.Do(() =>
            {
                this.chain.RemoveBlock(chainedHeader);
                this.chainVersion++;
            });
        }

        public int UnspentTxCount
        {
            get { return 0; }
        }

        public bool ContainsUnspentTx(UInt256 txHash)
        {
            return this.semaphore.Do(() =>
                this.unspentTransactions.ContainsKey(txHash));
        }

        public bool TryGetUnspentTx(UInt256 txHash, out UnspentTx unspentTx)
        {
            this.semaphore.Wait();
            try
            {
                return this.unspentTransactions.TryGetValue(txHash, out unspentTx);
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public bool TryAddUnspentTx(UnspentTx unspentTx)
        {
            return this.semaphore.Do(() =>
            {
                var wasAdded = this.unspentTransactions.TryAdd(unspentTx.TxHash, unspentTx);
                if (wasAdded)
                    this.unspentTxesVersion++;

                return wasAdded;
            });
        }

        public bool TryRemoveUnspentTx(UInt256 txHash)
        {
            return this.semaphore.Do(() =>
            {
                var wasRemoved = this.unspentTransactions.Remove(txHash);
                if (wasRemoved)
                    this.unspentTxesVersion++;

                return wasRemoved;
            });
        }

        public bool TryUpdateUnspentTx(UnspentTx unspentTx)
        {
            return this.semaphore.Do(() =>
            {
                if (this.unspentTransactions.ContainsKey(unspentTx.TxHash))
                {
                    this.unspentTransactions[unspentTx.TxHash] = unspentTx;
                    this.unspentTxesVersion++;
                    return true;
                }
                else
                {
                    return false;
                }
            });
        }

        public void PrepareSpentTransactions(int spentBlockIndex)
        {
            this.semaphore.Do(() =>
            {
                this.spentTransactions.Add(spentBlockIndex, new List<SpentTx>());
                this.spentTxesVersion++;
            });
        }

        public IEnumerable<UnspentTx> ReadUnspentTransactions()
        {
            return this.semaphore.Do(() =>
                this.unspentTransactions.ToImmutable()).Values;
        }

        public IEnumerable<SpentTx> ReadSpentTransactions(int spentBlockIndex)
        {
            return this.semaphore.Do(() =>
            {
                List<SpentTx> spentTxes;
                if (this.spentTransactions.TryGetValue(spentBlockIndex, out spentTxes))
                    return spentTxes.ToImmutableList();
                else
                    return Enumerable.Empty<SpentTx>();
            });
        }

        public void AddSpentTransaction(SpentTx spentTx)
        {
            this.semaphore.Do(() =>
            {
                this.spentTransactions[spentTx.SpentBlockIndex].Add(spentTx);
                this.spentTxesVersion++;
            });
        }

        public void RemoveSpentTransactions(int spentBlockIndex)
        {
            this.semaphore.Do(() =>
            {
                var wasRemoved = this.spentTransactions.Remove(spentBlockIndex);
                if (wasRemoved)
                    this.spentTxesVersion++;
            });
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            this.semaphore.Do(() =>
            {
                this.spentTransactions.RemoveWhere(x => x.Key <= spentBlockIndex);
                this.spentTxesVersion++;
            });
        }

        public void Defragment()
        {
        }
    }
}
