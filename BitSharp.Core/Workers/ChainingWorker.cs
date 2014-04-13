using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using NLog;
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

namespace BitSharp.Core.Workers
{
    public class ChainingWorker : Worker
    {
        private readonly IBlockchainRules rules;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly ChainedBlockCache chainedBlockCache;
        private readonly BlockCache blockCache;

        private readonly ConcurrentQueue<BlockHeader> blockHeaders;
        private readonly Dictionary<UInt256, Dictionary<UInt256, BlockHeader>> unchainedByPrevious;

        public ChainingWorker(WorkerConfig workerConfig, Logger logger, IBlockchainRules rules, BlockHeaderCache blockHeaderCache, ChainedBlockCache chainedBlockCache, BlockCache blockCache)
            : base("ChainingWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.rules = rules;
            this.blockHeaderCache = blockHeaderCache;
            this.chainedBlockCache = chainedBlockCache;
            this.blockCache = blockCache;

            this.blockHeaders = new ConcurrentQueue<BlockHeader>();
            this.unchainedByPrevious = new Dictionary<UInt256, Dictionary<UInt256, BlockHeader>>();

            this.blockHeaderCache.OnAddition += ChainBlockHeader;
            this.blockCache.OnAddition += ChainBlock;
        }

        protected override void SubDispose()
        {
            // unwire events
            this.blockHeaderCache.OnAddition -= ChainBlockHeader;
            this.blockCache.OnAddition -= ChainBlock;
        }

        public IReadOnlyDictionary<UInt256, IReadOnlyDictionary<UInt256, BlockHeader>> UnchainedByPrevious
        {
            get
            {
                return this.unchainedByPrevious.AsReadOnly();
            }
        }

        protected override void SubStart()
        {
            this.QueueAllBlockHeaders();
        }

        public void QueueAllBlockHeaders()
        {
            var thread = new Thread(
                () =>
                {
                    new MethodTimer().Time(() =>
                        this.blockHeaders.EnqueueRange(this.blockHeaderCache.Values));

                    this.NotifyWork();
                });

            thread.Name = "{0}.QueueAllBlockHeaders".Format2(this.Name);
            thread.Start();
        }

        protected override void WorkAction()
        {
            BlockHeader blockHeader;
            while (this.blockHeaders.TryDequeue(out blockHeader))
            {
                // cooperative loop
                this.ShutdownToken.Token.ThrowIfCancellationRequested();

                if (!this.chainedBlockCache.ContainsKey(blockHeader.Hash))
                {
                    ChainedBlock prevChainedBlock;
                    if (this.chainedBlockCache.TryGetValue(blockHeader.PreviousBlock, out prevChainedBlock))
                    {
                        this.chainedBlockCache[blockHeader.Hash] =
                            new ChainedBlock
                            (
                                blockHash: blockHeader.Hash,
                                previousBlockHash: blockHeader.PreviousBlock,
                                height: prevChainedBlock.Height + 1,
                                totalWork: prevChainedBlock.TotalWork + blockHeader.CalculateWork()
                            );

                        if (this.unchainedByPrevious.ContainsKey(blockHeader.Hash))
                        {
                            this.blockHeaders.EnqueueRange(this.unchainedByPrevious[blockHeader.Hash].Values);
                        }

                        if (this.unchainedByPrevious.ContainsKey(blockHeader.PreviousBlock))
                        {
                            this.unchainedByPrevious[blockHeader.PreviousBlock].Remove(blockHeader.Hash);
                            if (this.unchainedByPrevious[blockHeader.PreviousBlock].Count == 0)
                                this.unchainedByPrevious.Remove(blockHeader.PreviousBlock);
                        }
                    }
                    else
                    {
                        if (!this.unchainedByPrevious.ContainsKey(blockHeader.PreviousBlock))
                            this.unchainedByPrevious[blockHeader.PreviousBlock] = new Dictionary<UInt256, BlockHeader>();
                        this.unchainedByPrevious[blockHeader.PreviousBlock][blockHeader.Hash] = blockHeader;
                    }
                }
                else
                {
                    if (this.unchainedByPrevious.ContainsKey(blockHeader.Hash))
                    {
                        this.blockHeaders.EnqueueRange(this.unchainedByPrevious[blockHeader.Hash].Values);
                    }
                }
            }
        }

        private void ChainBlockHeader(UInt256 blockHash, BlockHeader blockHeader)
        {
            if (!this.chainedBlockCache.ContainsKey(blockHeader.Hash))
            {
                try
                {
                    if (blockHeader == null)
                        blockHeader = this.blockHeaderCache[blockHash];
                }
                catch (MissingDataException) { return; }

                this.blockHeaders.Enqueue(blockHeader);

                this.NotifyWork();
            }
        }

        private void ChainBlock(UInt256 blockHash, Block block)
        {
            if (block != null)
                this.blockHeaderCache.TryAdd(blockHash, block.Header);
        }
    }
}
