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
        private readonly ICacheContext cacheContext;
        private readonly Func<ChainState> getChainState;

        public PruningWorker(IBlockchainRules rules, ICacheContext cacheContext, Func<ChainState> getChainState, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base("PruningWorker", initialNotify, minIdleTime, maxIdleTime)
        {
            this.rules = rules;
            this.cacheContext = cacheContext;
            this.getChainState = getChainState;
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
                        if (this.cacheContext.BlockRollbackCache.TryGetValue(block.BlockHash, out blockRollbackInformation))
                        {
                            foreach (var keyPair in blockRollbackInformation)
                                this.cacheContext.TransactionCache.TryRemove(keyPair.Key);
                        }
                    }
                    break;

                case PruningMode.Full:
                    for (var i = 0; i < chainState.Chain.Blocks.Count /*- pruneBuffer*/; i++)
                    {
                        var block = chainState.Chain.Blocks[i];

                        IImmutableList<UInt256> blockTxHashes;
                        if (this.cacheContext.BlockTxHashesCache.TryGetValue(block.BlockHash, out blockTxHashes))
                        {
                            foreach (var txHash in blockTxHashes)
                                this.cacheContext.TransactionCache.TryRemove(txHash);
                        }
                    }
                    break;
            }

            this.cacheContext.TransactionCache.Flush();

            for (var i = 0; i < chainState.Chain.Blocks.Count - pruneBuffer; i++)
            {
                var block = chainState.Chain.Blocks[i];

                this.cacheContext.BlockTxHashesCache.TryRemove(block.BlockHash);
                this.cacheContext.BlockRollbackCache.TryRemove(block.BlockHash);
            }

            this.cacheContext.BlockTxHashesCache.Flush();
            this.cacheContext.BlockRollbackCache.Flush();
        }
    }

    public enum PruningMode
    {
        PreserveUnspentTranscations,
        Full
    }
}
