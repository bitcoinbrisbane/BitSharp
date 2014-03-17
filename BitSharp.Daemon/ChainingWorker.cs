using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon
{
    public class ChainingWorker : IDisposable
    {
        private readonly CacheContext cacheContext;

        private readonly IBlockchainRules rules;

        private readonly Dictionary<UInt256, Dictionary<UInt256, BlockHeader>> unchainedBlocksByPrevious;

        private readonly CancellationTokenSource shutdownToken;

        private readonly Worker chainBlocksWorker;

        private readonly ConcurrentQueue<BlockHeader> chainBlocksPending;

        public ChainingWorker(IBlockchainRules rules, CacheContext cacheContext)
        {
            this.shutdownToken = new CancellationTokenSource();

            this.rules = rules;
            this.cacheContext = cacheContext;

            this.chainBlocksPending = new ConcurrentQueue<BlockHeader>();

            this.unchainedBlocksByPrevious = new Dictionary<UInt256, Dictionary<UInt256, BlockHeader>>();

            // wire up cache events
            this.cacheContext.BlockHeaderCache.OnAddition += ChainBlockHeader;
            this.cacheContext.BlockHeaderCache.OnModification += ChainBlockHeader;
            this.cacheContext.BlockCache.OnAddition += ChainBlock;
            this.cacheContext.BlockCache.OnModification += ChainBlock;

            // create workers
            this.chainBlocksWorker = new Worker("TargetChainWorker.ChainBlocksWorker", ChainBlocksWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30));

            new Thread(
                () => ChainMissingBlocks())
                .Start();
        }

        public CacheContext CacheContext { get { return this.cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public void Start()
        {
            try
            {
                // start loading the existing state from storage
                //TODO LoadExistingState();

                // startup workers
                this.chainBlocksWorker.Start();
            }
            catch (Exception)
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            // cleanup events
            this.CacheContext.BlockHeaderCache.OnAddition -= ChainBlockHeader;
            this.CacheContext.BlockHeaderCache.OnModification -= ChainBlockHeader;
            this.CacheContext.BlockCache.OnAddition -= ChainBlock;
            this.CacheContext.BlockCache.OnModification -= ChainBlock;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
                this.chainBlocksWorker,
                this.shutdownToken
            }.DisposeList();
        }

        private void ChainMissingBlocks()
        {
            foreach (var unchainedBlockHash in
                this.CacheContext.BlockHeaderCache.Keys
                .Except(this.CacheContext.ChainedBlockCache.Keys))
            {
                BlockHeader unchainedBlockHeader;
                if (this.CacheContext.BlockHeaderCache.TryGetValue(unchainedBlockHash, out unchainedBlockHeader))
                {
                    ChainBlockHeader(unchainedBlockHash, unchainedBlockHeader);
                }
            }
        }

        private void ChainBlockHeader(UInt256 blockHash, BlockHeader blockHeader)
        {
            try
            {
                if (blockHeader == null)
                    blockHeader = this.CacheContext.BlockHeaderCache[blockHash];
            }
            catch (MissingDataException) { return; }

            this.chainBlocksPending.Enqueue(blockHeader);
            this.chainBlocksWorker.NotifyWork();
        }

        private void ChainBlock(UInt256 blockHash, Block block)
        {
            ChainBlockHeader(blockHash, block != null ? block.Header : null);
        }

        private void ChainBlocksWorker()
        {
            BlockHeader workBlock;
            while (this.chainBlocksPending.TryDequeue(out workBlock))
            {
                // cooperative loop
                if (this.shutdownToken.IsCancellationRequested)
                    return;

                if (!this.CacheContext.ChainedBlockCache.ContainsKey(workBlock.Hash))
                {
                    ChainedBlock prevChainedBlock;
                    if (this.CacheContext.ChainedBlockCache.TryGetValue(workBlock.PreviousBlock, out prevChainedBlock))
                    {
                        var newChainedBlock = new ChainedBlock
                        (
                            blockHash: workBlock.Hash,
                            previousBlockHash: workBlock.PreviousBlock,
                            height: prevChainedBlock.Height + 1,
                            totalWork: prevChainedBlock.TotalWork + workBlock.CalculateWork()
                        );

                        this.CacheContext.ChainedBlockCache[workBlock.Hash] = newChainedBlock;

                        if (this.unchainedBlocksByPrevious.ContainsKey(workBlock.Hash))
                        {
                            this.chainBlocksPending.EnqueueRange(this.unchainedBlocksByPrevious[workBlock.Hash].Values);
                        }

                        if (this.unchainedBlocksByPrevious.ContainsKey(workBlock.PreviousBlock))
                        {
                            this.unchainedBlocksByPrevious[workBlock.PreviousBlock].Remove(workBlock.Hash);
                            if (this.unchainedBlocksByPrevious[workBlock.PreviousBlock].Count == 0)
                                this.unchainedBlocksByPrevious.Remove(workBlock.PreviousBlock);
                        }
                    }
                    else
                    {
                        if (!this.unchainedBlocksByPrevious.ContainsKey(workBlock.PreviousBlock))
                            this.unchainedBlocksByPrevious[workBlock.PreviousBlock] = new Dictionary<UInt256, BlockHeader>();
                        this.unchainedBlocksByPrevious[workBlock.PreviousBlock][workBlock.Hash] = workBlock;
                    }
                }
                else
                {
                    if (this.unchainedBlocksByPrevious.ContainsKey(workBlock.Hash))
                    {
                        this.chainBlocksPending.EnqueueRange(this.unchainedBlocksByPrevious[workBlock.Hash].Values);
                    }
                }
            }
        }
    }
}
