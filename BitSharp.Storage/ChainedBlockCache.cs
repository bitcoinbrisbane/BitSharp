using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class ChainedBlockCache : BoundedCache<UInt256, ChainedBlock>
    {
        private readonly CacheContext _cacheContext;
        private readonly ConcurrentSetBuilder<UInt256> leafChainedBlocks;
        private readonly ConcurrentDictionary<UInt256, ConcurrentSet<UInt256>> chainedBlocksByPrevious;

        private BigInteger maxTotalWork;
        private ImmutableList<UInt256> maxTotalWorkBlocks;
        private readonly ReaderWriterLockSlim maxTotalWorkLock;

        public ChainedBlockCache(CacheContext cacheContext, long maxFlushMemorySize, long maxCacheMemorySize)
            : base("ChainedBlockCache", cacheContext.StorageContext.ChainedBlockStorage, maxFlushMemorySize, maxCacheMemorySize, ChainedBlock.SizeEstimator)
        {
            this._cacheContext = cacheContext;

            this.leafChainedBlocks = new ConcurrentSetBuilder<UInt256>();
            this.chainedBlocksByPrevious = new ConcurrentDictionary<UInt256, ConcurrentSet<UInt256>>();

            this.OnAddition += blockHash => UpdatePreviousIndex(blockHash);
            this.OnModification += (blockHash, chainedBlock) => UpdatePreviousIndex(chainedBlock);
            this.OnRetrieved += (blockHash, chainedBlock) => UpdatePreviousIndex(chainedBlock);

            foreach (var value in this.StreamAllValues())
                UpdatePreviousIndex(value.Value);

            this.maxTotalWork = -1;
            this.maxTotalWorkBlocks = ImmutableList.Create<UInt256>();
            this.maxTotalWorkLock = new ReaderWriterLockSlim();

            //TODO period rescan
            var checkThread = new Thread(() =>
            {
                foreach (var keyPair in this._cacheContext.StorageContext.ChainedBlockStorage.SelectMaxTotalWorkBlocks())
                    CheckTotalWork(keyPair.Key, keyPair.Value);
            });
            checkThread.Start();
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public bool IsChainIntact(UInt256 blockHash)
        {
            ChainedBlock chainedBlock;
            if (TryGetValue(blockHash, out chainedBlock))
            {
                return IsChainIntact(chainedBlock);
            }
            else
            {
                return false;
            }
        }

        public bool IsChainIntact(ChainedBlock chainedBlock)
        {
            // look backwards until height 0 is reached
            var expectedHeight = chainedBlock.Height;
            while (chainedBlock.Height != 0)
            {
                // if a missing link occurrs before height 0, the chain isn't intact
                if (!this.ContainsKey(chainedBlock.PreviousBlockHash)
                    || !TryGetValue(chainedBlock.PreviousBlockHash, out chainedBlock))
                {
                    return false;
                }

                expectedHeight--;
                if (chainedBlock.Height != expectedHeight)
                {
                    Debugger.Break();
                    return false;
                }
            }

            // height 0 reached, chain is intact
            return true;
        }

        public bool TryGetChain(UInt256 blockHash, out List<ChainedBlock> chain)
        {
            ChainedBlock chainedBlock;
            if (TryGetValue(blockHash, out chainedBlock))
            {
                return TryGetChain(chainedBlock, out chain);
            }
            else
            {
                chain = null;
                return false;
            }
        }

        public bool TryGetChain(ChainedBlock chainedBlock, out List<ChainedBlock> chain)
        {
            chain = new List<ChainedBlock>(chainedBlock.Height);

            // look backwards until height 0 is reached
            var expectedHeight = chainedBlock.Height;
            while (chainedBlock.Height != 0)
            {
                chain.Add(chainedBlock);

                // if a missing link occurrs before height 0, the chain isn't intact
                if (!this.ContainsKey(chainedBlock.PreviousBlockHash)
                    || !TryGetValue(chainedBlock.PreviousBlockHash, out chainedBlock))
                {
                    chain = null;
                    return false;
                }

                expectedHeight--;
                if (chainedBlock.Height != expectedHeight)
                {
                    Debugger.Break();
                    chain = null;
                    return false;
                }
            }
            chain.Add(chainedBlock);
            chain.Reverse();

            // height 0 reached, chain is intact
            return true;
        }

        public IEnumerable<ChainedBlock> FindLeafChainedBlocks()
        {
            var leafChainedBlocksLocal = this.leafChainedBlocks.ToImmutable();

            foreach (var leafChainedBlockHash in leafChainedBlocks)
            {
                ChainedBlock leafChainedBlock;
                if (this.TryGetValue(leafChainedBlockHash, out leafChainedBlock)
                    /*&& this.IsChainIntact(leafChainedBlock)*/)
                {
                    yield return leafChainedBlock;
                }
            }
        }

        public IEnumerable<List<ChainedBlock>> FindLeafChains()
        {
            var leafChainedBlocks = new HashSet<UInt256>(this.GetAllKeys());
            leafChainedBlocks.ExceptWith(this.chainedBlocksByPrevious.Keys);

            foreach (var leafChainedBlock in leafChainedBlocks)
            {
                List<ChainedBlock> leafChain;
                if (this.TryGetChain(leafChainedBlock, out leafChain))
                {
                    yield return leafChain;
                }
            }
        }

        public HashSet<UInt256> FindByPreviousBlockHash(UInt256 previousBlockHash)
        {
            ConcurrentSet<UInt256> set;
            if (this.chainedBlocksByPrevious.TryGetValue(previousBlockHash, out set))
            {
                return new HashSet<UInt256>(set);
            }
            else
            {
                return new HashSet<UInt256>();
            }
        }

        public IImmutableList<UInt256> MaxTotalWorkBlocks
        {
            get
            {
                return this.maxTotalWorkLock.DoRead(() => this.maxTotalWorkBlocks.ToImmutableList());
            }
        }

        public override void CreateValue(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            CheckTotalWork(blockHash, chainedBlock);
            base.CreateValue(blockHash, chainedBlock);
        }

        public override void UpdateValue(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            CheckTotalWork(blockHash, chainedBlock);
            base.UpdateValue(blockHash, chainedBlock);
        }

        private void CheckTotalWork(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            this.maxTotalWorkLock.DoWrite(() =>
            {
                if (chainedBlock.TotalWork > this.maxTotalWork)
                {
                    this.maxTotalWork = chainedBlock.TotalWork;
                    this.maxTotalWorkBlocks = ImmutableList.Create<UInt256>(blockHash);
                }
                else if (chainedBlock.TotalWork == this.maxTotalWork)
                {
                    this.maxTotalWorkBlocks = this.maxTotalWorkBlocks.Add(blockHash);
                }
            });
        }

        private void UpdatePreviousIndex(UInt256 blockHash)
        {
            ChainedBlock chainedBlock;
            if (this.TryGetValue(blockHash, out chainedBlock))
                UpdatePreviousIndex(chainedBlock);
        }

        private void UpdatePreviousIndex(ChainedBlock chainedBlock)
        {
            this.chainedBlocksByPrevious.AddOrUpdate
            (
                chainedBlock.PreviousBlockHash,
                newKey => new ConcurrentSet<UInt256>(),
                (existingKey, existingValue) => existingValue
            )
            .Add(chainedBlock.BlockHash);

            //TODO better thread safety
            if (!this.chainedBlocksByPrevious.ContainsKey(chainedBlock.BlockHash))
            {
                this.leafChainedBlocks.Add(chainedBlock.BlockHash);
                if (this.chainedBlocksByPrevious.ContainsKey(chainedBlock.BlockHash))
                {
                    this.leafChainedBlocks.Remove(chainedBlock.BlockHash);
                    Debugger.Break();
                }
            }

            this.leafChainedBlocks.Remove(chainedBlock.PreviousBlockHash);
        }
    }
}
