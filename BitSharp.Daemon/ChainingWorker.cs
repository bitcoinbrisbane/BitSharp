using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
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

namespace BitSharp.Daemon
{
    public class ChainingWorker : Worker
    {
        private readonly IBlockchainRules rules;
        private readonly CacheContext cacheContext;
        private readonly ConcurrentQueue<BlockHeader> blockHeaders;
        private readonly Dictionary<UInt256, Dictionary<UInt256, BlockHeader>> unchainedBlocksByPrevious;

        public ChainingWorker(IBlockchainRules rules, CacheContext cacheContext, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base("ChainingWorker", initialNotify, minIdleTime, maxIdleTime)
        {
            this.rules = rules;
            this.cacheContext = cacheContext;
            this.blockHeaders = new ConcurrentQueue<BlockHeader>();
            this.unchainedBlocksByPrevious = new Dictionary<UInt256, Dictionary<UInt256, BlockHeader>>();

            this.cacheContext.BlockHeaderCache.OnAddition += ChainBlockHeader;
            this.QueueAllBlockHeaders();
        }

        protected override void SubDispose()
        {
            // unwire events
            this.cacheContext.BlockHeaderCache.OnAddition -= ChainBlockHeader;
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
                        this.blockHeaders.EnqueueRange(this.cacheContext.BlockHeaderCache.Values));
                    
                    this.NotifyWork();
                })
                .Start();
        }

        protected override void WorkAction()
        {
            BlockHeader blockHeader;
            while (this.blockHeaders.TryDequeue(out blockHeader))
            {
                // cooperative loop
                this.ShutdownToken.Token.ThrowIfCancellationRequested();

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

                this.NotifyWork();
            }
        }

        private void ChainBlock(UInt256 blockHash, Block block)
        {
            ChainBlockHeader(blockHash, block != null ? block.Header : null);
        }
    }
}
