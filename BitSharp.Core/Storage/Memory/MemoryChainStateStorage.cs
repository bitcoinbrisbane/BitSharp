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
        private ImmutableDictionary<UInt256, IImmutableList<UnmintedTx>>.Builder blockUnmintedTxes;

        private long chainVersion;
        private long unspentTxesVersion;
        private long blockSpentTxesVersion;
        private long blockUnmintedTxesVersion;

        public MemoryChainStateStorage(Chain chain = null, ImmutableSortedDictionary<UInt256, UnspentTx> unspentTransactions = null, ImmutableDictionary<int, IImmutableList<SpentTx>> blockSpentTxes = null, ImmutableDictionary<UInt256, IImmutableList<UnmintedTx>> blockUnmintedTxes = null)
        {
            this.chain = chain != null ? chain.ToBuilder() : new ChainBuilder();
            this.unspentTransactions = unspentTransactions != null ? unspentTransactions.ToBuilder() : ImmutableSortedDictionary.CreateBuilder<UInt256, UnspentTx>();
            this.blockSpentTxes = blockSpentTxes != null ? blockSpentTxes.ToBuilder() : ImmutableDictionary.CreateBuilder<int, IImmutableList<SpentTx>>();
            this.blockUnmintedTxes = blockUnmintedTxes != null ? blockUnmintedTxes.ToBuilder() : ImmutableDictionary.CreateBuilder<UInt256, IImmutableList<UnmintedTx>>();
        }

        public void Dispose()
        {
        }

        public void BeginTransaction(out ChainBuilder chain, out ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions, out ImmutableDictionary<int, IImmutableList<SpentTx>>.Builder blockSpentTxes, out ImmutableDictionary<UInt256, IImmutableList<UnmintedTx>>.Builder blockUnmintedTxes, out long chainVersion, out long unspentTxesVersion, out long spentTxesVersion, out long unmintedTxesVersion)
        {
            this.semaphore.Wait();
            try
            {
                chain = this.chain.ToImmutable().ToBuilder();
                unspentTransactions = this.unspentTransactions.ToImmutable().ToBuilder();
                blockSpentTxes = this.blockSpentTxes.ToImmutable().ToBuilder();
                blockUnmintedTxes = this.blockUnmintedTxes.ToImmutable().ToBuilder();

                chainVersion = this.chainVersion;
                unspentTxesVersion = this.unspentTxesVersion;
                spentTxesVersion = this.blockSpentTxesVersion;
                unmintedTxesVersion = this.blockUnmintedTxesVersion;
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public void CommitTransaction(ChainBuilder chain, ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions, ImmutableDictionary<int, IImmutableList<SpentTx>>.Builder blockSpentTxes, ImmutableDictionary<UInt256, IImmutableList<UnmintedTx>>.Builder blockUnmintedTxes, long chainVersion, long unspentTxesVersion, long blockSpentTxesVersion, long blockUnmintedTxesVersion)
        {
            this.semaphore.Do(() =>
            {
                if (chain != null && this.chainVersion != chainVersion
                    || unspentTransactions != null && unspentTxesVersion != this.unspentTxesVersion
                    || blockSpentTxes != null && blockSpentTxesVersion != this.blockSpentTxesVersion
                    || blockUnmintedTxes != null && blockUnmintedTxesVersion != this.blockUnmintedTxesVersion)
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

                if (blockUnmintedTxes != null)
                {
                    this.blockUnmintedTxes = blockUnmintedTxes.ToImmutable().ToBuilder();
                    this.blockUnmintedTxesVersion++;
                }
            });
        }

        public IEnumerable<ChainedHeader> ReadChain()
        {
            return this.semaphore.Do(() =>
                this.chain.ToImmutable()).Blocks;
        }

        public ChainedHeader GetChainTip()
        {
            return this.semaphore.Do(() =>
                this.chain.LastBlock);
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
            get
            {
                return this.semaphore.Do(() =>
                    this.unspentTransactions.Count);
            }
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

        public bool ContainsBlockUnmintedTxes(UInt256 blockHash)
        {
            return this.semaphore.Do(() =>
                this.blockUnmintedTxes.ContainsKey(blockHash));
        }

        public bool TryGetBlockUnmintedTxes(UInt256 blockHash, out IImmutableList<UnmintedTx> unmintedTxes)
        {
            this.semaphore.Wait();
            try
            {
                return this.blockUnmintedTxes.TryGetValue(blockHash, out unmintedTxes);
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        public bool TryAddBlockUnmintedTxes(UInt256 blockHash, IImmutableList<UnmintedTx> unmintedTxes)
        {
            return this.semaphore.Do(() =>
            {
                try
                {
                    this.blockUnmintedTxes.Add(blockHash, ImmutableArray.CreateRange(unmintedTxes));
                    this.blockUnmintedTxesVersion++;
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            });
        }

        public bool TryRemoveBlockUnmintedTxes(UInt256 blockHash)
        {
            return this.semaphore.Do(() =>
            {
                var wasRemoved = this.blockUnmintedTxes.Remove(blockHash);
                if (wasRemoved)
                    this.blockUnmintedTxesVersion++;

                return wasRemoved;
            });
        }

        public void Defragment()
        {
        }
    }
}
