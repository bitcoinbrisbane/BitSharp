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
        private ImmutableDictionary<int, IImmutableList<SpentTx>>.Builder blockSpentTxes;

        private long chainVersion;
        private long unspentTxesVersion;
        private long blockSpentTxesVersion;

        public MemoryChainStateStorage(Chain chain = null, ImmutableSortedDictionary<UInt256, UnspentTx> unspentTransactions = null, ImmutableDictionary<int, IImmutableList<SpentTx>> blockSpentTxes = null)
        {
            this.chain = chain != null ? chain.ToBuilder() : new ChainBuilder();
            this.unspentTransactions = unspentTransactions != null ? unspentTransactions.ToBuilder() : ImmutableSortedDictionary.CreateBuilder<UInt256, UnspentTx>();
            this.blockSpentTxes = blockSpentTxes != null ? blockSpentTxes.ToBuilder() : ImmutableDictionary.CreateBuilder<int, IImmutableList<SpentTx>>();
        }

        public void Dispose()
        {
        }

        public void BeginTransaction(out ChainBuilder chain, out ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions, out ImmutableDictionary<int, IImmutableList<SpentTx>>.Builder blockSpentTxes, out long chainVersion, out long unspentTxesVersion, out long spentTxesVersion)
        {
            this.semaphore.Wait();
            try
            {
                chain = this.chain.ToImmutable().ToBuilder();
                unspentTransactions = this.unspentTransactions.ToImmutable().ToBuilder();
                blockSpentTxes = this.blockSpentTxes.ToImmutable().ToBuilder();

                chainVersion = this.chainVersion;
                unspentTxesVersion = this.unspentTxesVersion;
                spentTxesVersion = this.blockSpentTxesVersion;
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public void CommitTransaction(ChainBuilder chain, ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions, ImmutableDictionary<int, IImmutableList<SpentTx>>.Builder blockSpentTxes, long chainVersion, long unspentTxesVersion, long blockSpentTxesVersion)
        {
            this.semaphore.Do(() =>
            {
                if (chain != null && this.chainVersion != chainVersion
                    || unspentTransactions != null && unspentTxesVersion != this.unspentTxesVersion
                    || blockSpentTxes != null && blockSpentTxesVersion != this.blockSpentTxesVersion)
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

                if (blockSpentTxes != null)
                {
                    this.blockSpentTxes = blockSpentTxes.ToImmutable().ToBuilder();
                    this.blockSpentTxesVersion++;
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

        public IEnumerable<UnspentTx> ReadUnspentTransactions()
        {
            return this.semaphore.Do(() =>
                this.unspentTransactions.ToImmutable()).Values;
        }

        public bool ContainsBlockSpentTxes(int blockIndex)
        {
            return this.semaphore.Do(() =>
                this.blockSpentTxes.ContainsKey(blockIndex));
        }


        public bool TryGetBlockSpentTxes(int blockIndex, out IImmutableList<SpentTx> spentTxes)
        {
            this.semaphore.Wait();
            try
            {
                return this.blockSpentTxes.TryGetValue(blockIndex, out spentTxes);
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public bool TryAddBlockSpentTxes(int blockIndex, IImmutableList<SpentTx> spentTxes)
        {
            return this.semaphore.Do(() =>
            {
                try
                {
                    this.blockSpentTxes.Add(blockIndex, ImmutableArray.CreateRange(spentTxes));
                    this.blockSpentTxesVersion++;
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            });
        }

        public bool TryRemoveBlockSpentTxes(int blockIndex)
        {
            return this.semaphore.Do(() =>
            {
                var wasRemoved = this.blockSpentTxes.Remove(blockIndex);
                if (wasRemoved)
                    this.blockSpentTxesVersion++;

                return wasRemoved;
            });
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            this.semaphore.Do(() =>
            {
                this.blockSpentTxes.RemoveRange(Enumerable.Range(0, spentBlockIndex));
                this.blockSpentTxesVersion++;
            });
        }

        public void Defragment()
        {
        }
    }
}
