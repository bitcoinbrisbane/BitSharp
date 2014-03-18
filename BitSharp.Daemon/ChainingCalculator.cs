using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon
{
    internal class ChainingCalculator : IDisposable
    {
        public event Action OnQueued;

        private readonly CacheContext cacheContext;
        private readonly ConcurrentQueue<BlockHeader> blockHeaders;
        private readonly Dictionary<UInt256, Dictionary<UInt256, BlockHeader>> unchainedBlocksByPrevious;
        private readonly SemaphoreSlim workSemaphore;

        public ChainingCalculator(CacheContext cacheContext)
        {
            this.cacheContext = cacheContext;
            this.blockHeaders = new ConcurrentQueue<BlockHeader>();
            this.unchainedBlocksByPrevious = new Dictionary<UInt256, Dictionary<UInt256, BlockHeader>>();
            this.workSemaphore = new SemaphoreSlim(1);

            // wire up cache events
            this.cacheContext.BlockHeaderCache.OnAddition += ChainBlockHeader;
            //this.cacheContext.BlockHeaderCache.OnModification += ChainBlockHeader;
            //this.cacheContext.BlockCache.OnAddition += ChainBlock;
            //this.cacheContext.BlockCache.OnModification += ChainBlock;
        }

        public void Dispose()
        {
            // cleanup events
            this.cacheContext.BlockHeaderCache.OnAddition -= ChainBlockHeader;
            //this.cacheContext.BlockHeaderCache.OnModification -= ChainBlockHeader;
            //this.cacheContext.BlockCache.OnAddition -= ChainBlock;
            //this.cacheContext.BlockCache.OnModification -= ChainBlock;
        }

        public IReadOnlyDictionary<UInt256, IReadOnlyDictionary<UInt256, BlockHeader>> UnchainedBlocksByPrevious
        {
            get
            {
                return this.unchainedBlocksByPrevious.AsReadOnly();
            }
        }

        public void QueueAllBlockHeaders()
        {
            new Thread(
                () =>
                {
                    new MethodTimer().Time(() =>
                    {
                        this.blockHeaders.EnqueueRange(this.cacheContext.BlockHeaderCache.Values);

                        var handler = this.OnQueued;
                        if (handler != null)
                            handler();
                    });
                })
                .Start();
        }

        public void ChainBlockHeaders(CancellationToken? cancelToken = null)
        {
            this.workSemaphore.Do(() =>
            {
                BlockHeader blockHeader;
                while (this.blockHeaders.TryDequeue(out blockHeader))
                {
                    // cooperative loop
                    cancelToken.GetValueOrDefault(CancellationToken.None).ThrowIfCancellationRequested();

                    if (!this.cacheContext.ChainedBlockCache.ContainsKey(blockHeader.Hash))
                    {
                        ChainedBlock prevChainedBlock;
                        if (this.cacheContext.ChainedBlockCache.TryGetValue(blockHeader.PreviousBlock, out prevChainedBlock))
                        {
                            this.cacheContext.ChainedBlockCache[blockHeader.Hash] =
                                new ChainedBlock
                                (
                                    blockHash: blockHeader.Hash,
                                    previousBlockHash: blockHeader.PreviousBlock,
                                    height: prevChainedBlock.Height + 1,
                                    totalWork: prevChainedBlock.TotalWork + blockHeader.CalculateWork()
                                );

                            if (this.unchainedBlocksByPrevious.ContainsKey(blockHeader.Hash))
                            {
                                this.blockHeaders.EnqueueRange(this.unchainedBlocksByPrevious[blockHeader.Hash].Values);
                            }

                            if (this.unchainedBlocksByPrevious.ContainsKey(blockHeader.PreviousBlock))
                            {
                                this.unchainedBlocksByPrevious[blockHeader.PreviousBlock].Remove(blockHeader.Hash);
                                if (this.unchainedBlocksByPrevious[blockHeader.PreviousBlock].Count == 0)
                                    this.unchainedBlocksByPrevious.Remove(blockHeader.PreviousBlock);
                            }
                        }
                        else
                        {
                            if (!this.unchainedBlocksByPrevious.ContainsKey(blockHeader.PreviousBlock))
                                this.unchainedBlocksByPrevious[blockHeader.PreviousBlock] = new Dictionary<UInt256, BlockHeader>();
                            this.unchainedBlocksByPrevious[blockHeader.PreviousBlock][blockHeader.Hash] = blockHeader;
                        }
                    }
                    else
                    {
                        if (this.unchainedBlocksByPrevious.ContainsKey(blockHeader.Hash))
                        {
                            this.blockHeaders.EnqueueRange(this.unchainedBlocksByPrevious[blockHeader.Hash].Values);
                        }
                    }
                }
            });
        }

        private void ChainBlockHeader(UInt256 blockHash, BlockHeader blockHeader)
        {
            if (!this.cacheContext.ChainedBlockCache.ContainsKey(blockHeader.Hash))
            {
                try
                {
                    if (blockHeader == null)
                        blockHeader = this.cacheContext.BlockHeaderCache[blockHash];
                }
                catch (MissingDataException) { return; }

                this.blockHeaders.Enqueue(blockHeader);

                var handler = this.OnQueued;
                if (handler != null)
                    handler();
            }
        }

        private void ChainBlock(UInt256 blockHash, Block block)
        {
            ChainBlockHeader(blockHash, block != null ? block.Header : null);
        }
    }
}
