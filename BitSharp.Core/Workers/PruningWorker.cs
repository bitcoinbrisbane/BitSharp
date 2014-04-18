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
        private readonly Func<ChainState> getChainState;
        private readonly Func<ChainStateBuilder> getChainStateBuilder;
        private readonly IBlockchainRules rules;
        private readonly BlockTxHashesCache blockTxHashesCache;
        private readonly TransactionCache transactionCache;
        private readonly SpentTransactionsCache spentTransactionsCache;
        private readonly SpentOutputsCache spentOutputsCache;

        public PruningWorker(WorkerConfig workerConfig, Func<ChainState> getChainState, Func<ChainStateBuilder> getChainStateBuilder, Logger logger, IBlockchainRules rules, BlockTxHashesCache blockTxHashesCache, TransactionCache transactionCache, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
            : base("PruningWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.getChainState = getChainState;
            this.getChainStateBuilder = getChainStateBuilder;
            this.rules = rules;
            this.blockTxHashesCache = blockTxHashesCache;
            this.transactionCache = transactionCache;
            this.spentTransactionsCache = spentTransactionsCache;
            this.spentOutputsCache = spentOutputsCache;

            this.Mode = PruningMode.Full;
        }

        public PruningMode Mode { get; set; }

        protected override void WorkAction()
        {
            var chainState = this.getChainState();
            if (chainState == null)
                return;
            var chainStateBuilder = this.getChainStateBuilder();

            // prune committed chain
            var committedChain = chainState.Chain;
            PruneChain(committedChain);

            // prune builder chain
            if (chainStateBuilder != null)
            {
                try
                {
                    var builderChain = chainStateBuilder.Chain.ToImmutable();
                    PruneChain(builderChain, minHeight: committedChain.Height + 1);
                }
                catch (ObjectDisposedException) {/*chainStateBuilder may have been disposed after we grabbed a reference to it*/}
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
                        var block = chain.Blocks[i];

                        IImmutableList<KeyValuePair<UInt256, SpentTx>> spentTransactions;
                        if (this.spentTransactionsCache.TryGetValue(block.Hash, out spentTransactions))
                        {
                            foreach (var keyPair in spentTransactions)
                                this.transactionCache.TryRemove(keyPair.Key);
                        }
                    }
                    break;

                case PruningMode.Full:
                    for (var i = minHeight; i < chain.Blocks.Count - pruneBuffer; i++)
                    {
                        var block = chain.Blocks[i];

                        IImmutableList<UInt256> blockTxHashes;
                        if (this.blockTxHashesCache.TryGetValue(block.Hash, out blockTxHashes))
                        {
                            foreach (var txHash in blockTxHashes)
                                this.transactionCache.TryRemove(txHash);
                        }
                    }
                    break;
            }

            for (var i = minHeight; i < chain.Blocks.Count - pruneBuffer; i++)
            {
                var block = chain.Blocks[i];

                this.blockTxHashesCache.TryRemove(block.Hash);
                this.spentTransactionsCache.TryRemove(block.Hash);
                this.spentOutputsCache.TryRemove(block.Hash);
            }
        }
    }

    public enum PruningMode
    {
        PreserveUnspentTranscations,
        Full
    }
}
