using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Builders;
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
    public class PruningWorker : Worker
    {
        private readonly Func<ChainStateBuilder> getChainStateBuilder;
        private readonly IBlockchainRules rules;
        private readonly BlockTxHashesCache blockTxHashesCache;
        private readonly TransactionCache transactionCache;
        private readonly SpentTransactionsCache spentTransactionsCache;
        private readonly SpentOutputsCache spentOutputsCache;

        public PruningWorker(WorkerConfig workerConfig, Func<ChainStateBuilder> getChainStateBuilder, Logger logger, IBlockchainRules rules, BlockTxHashesCache blockTxHashesCache, TransactionCache transactionCache, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
            : base("PruningWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.getChainStateBuilder = getChainStateBuilder;
            this.rules = rules;
            this.blockTxHashesCache = blockTxHashesCache;
            this.transactionCache = transactionCache;
            this.spentTransactionsCache = spentTransactionsCache;
            this.spentOutputsCache = spentOutputsCache;

            this.Mode = PruningMode.SpentOnly;
        }

        public PruningMode Mode { get; set; }

        protected override void WorkAction()
        {
            var chainStateBuilder = this.getChainStateBuilder();

            // prune builder chain
            if (chainStateBuilder != null)
            {
                var builderChain = chainStateBuilder.Chain.ToImmutable();
                PruneChain(builderChain);
            }

            this.transactionCache.Flush();
            this.blockTxHashesCache.Flush();
            this.spentTransactionsCache.Flush();
            this.spentOutputsCache.Flush();
        }

        private void PruneChain(Chain chain, int minHeight = 0)
        {
            var blocksPerDay = 144;
            var pruneBuffer = blocksPerDay * 7;

            switch (this.Mode)
            {
                case PruningMode.PreserveUnspentTranscations:
                    for (var i = minHeight; i < chain.Blocks.Count - pruneBuffer; i++)
                    {
                        // cooperative loop
                        this.ThrowIfCancelled();

                        var block = chain.Blocks[i];

                        IImmutableList<KeyValuePair<UInt256, SpentTx>> spentTransactions;
                        if (this.spentTransactionsCache.TryGetValue(block.Hash, out spentTransactions))
                        {
                            foreach (var keyPair in spentTransactions)
                                this.transactionCache.TryRemove(keyPair.Key);
                        }
                    }

                    for (var i = minHeight; i < chain.Blocks.Count - pruneBuffer; i++)
                    {
                        // cooperative loop
                        this.ThrowIfCancelled();

                        var block = chain.Blocks[i];

                        this.blockTxHashesCache.TryRemove(block.Hash);
                        this.spentTransactionsCache.TryRemove(block.Hash);
                        this.spentOutputsCache.TryRemove(block.Hash);
                    }
                    break;

                case PruningMode.SpentOnly:
                    for (var i = minHeight; i < chain.Blocks.Count - pruneBuffer; i++)
                    {
                        // cooperative loop
                        this.ThrowIfCancelled();
                        
                        var block = chain.Blocks[i];

                        this.spentTransactionsCache.TryRemove(block.Hash);
                        this.spentOutputsCache.TryRemove(block.Hash);
                    }
                    break;

                case PruningMode.Full:
                    for (var i = minHeight; i < chain.Blocks.Count - pruneBuffer; i++)
                    {
                        // cooperative loop
                        this.ThrowIfCancelled();

                        var block = chain.Blocks[i];

                        IImmutableList<UInt256> blockTxHashes;
                        if (this.blockTxHashesCache.TryGetValue(block.Hash, out blockTxHashes))
                        {
                            foreach (var txHash in blockTxHashes)
                                this.transactionCache.TryRemove(txHash);
                        }
                    }

                    for (var i = minHeight; i < chain.Blocks.Count - pruneBuffer; i++)
                    {
                        // cooperative loop
                        this.ThrowIfCancelled();

                        var block = chain.Blocks[i];

                        this.blockTxHashesCache.TryRemove(block.Hash);
                        this.spentTransactionsCache.TryRemove(block.Hash);
                        this.spentOutputsCache.TryRemove(block.Hash);
                    }
                    break;
            }
        }
    }

    public enum PruningMode
    {
        PreserveUnspentTranscations,
        SpentOnly,
        Full
    }
}
