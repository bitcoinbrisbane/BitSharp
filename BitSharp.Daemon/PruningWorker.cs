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
    public class PruningWorker : Worker
    {
        private readonly IBlockchainRules rules;
        private readonly Func<ChainState> getChainState;
        private readonly BlockTxHashesCache blockTxHashesCache;
        private readonly TransactionCache transactionCache;
        private readonly BlockRollbackCache blockRollbackCache;

        public PruningWorker(Func<ChainState> getChainState, IBlockchainRules rules, BlockTxHashesCache blockTxHashesCache, TransactionCache transactionCache, BlockRollbackCache blockRollbackCache)
            : base("PruningWorker", initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.FromMinutes(5))
        {
            this.getChainState = getChainState;
            this.rules = rules;
            this.blockTxHashesCache = blockTxHashesCache;
            this.transactionCache = transactionCache;
            this.blockRollbackCache = blockRollbackCache;

            this.Mode = PruningMode.Full;
        }

        public PruningMode Mode { get; set; }

        protected override void WorkAction()
        {
            var chainState = this.getChainState();
            if (chainState == null)
                return;

            var blocksPerDay = 144;
            var pruneBuffer = blocksPerDay * 7;

            switch (this.Mode)
            {
                case PruningMode.PreserveUnspentTranscations:
                    for (var i = 0; i < chainState.Chain.Blocks.Count - pruneBuffer; i++)
                    {
                        var block = chainState.Chain.Blocks[i];

                        IImmutableList<KeyValuePair<UInt256, UInt256>> blockRollbackInformation;
                        if (this.blockRollbackCache.TryGetValue(block.BlockHash, out blockRollbackInformation))
                        {
                            foreach (var keyPair in blockRollbackInformation)
                                this.transactionCache.TryRemove(keyPair.Key);
                        }
                    }
                    break;

                case PruningMode.Full:
                    for (var i = 0; i < chainState.Chain.Blocks.Count /*- pruneBuffer*/; i++)
                    {
                        var block = chainState.Chain.Blocks[i];

                        IImmutableList<UInt256> blockTxHashes;
                        if (this.blockTxHashesCache.TryGetValue(block.BlockHash, out blockTxHashes))
                        {
                            foreach (var txHash in blockTxHashes)
                                this.transactionCache.TryRemove(txHash);
                        }
                    }
                    break;
            }

            this.transactionCache.Flush();

            for (var i = 0; i < chainState.Chain.Blocks.Count - pruneBuffer; i++)
            {
                var block = chainState.Chain.Blocks[i];

                this.blockTxHashesCache.TryRemove(block.BlockHash);
                this.blockRollbackCache.TryRemove(block.BlockHash);
            }

            this.blockTxHashesCache.Flush();
            this.blockRollbackCache.Flush();
        }
    }

    public enum PruningMode
    {
        PreserveUnspentTranscations,
        Full
    }
}
