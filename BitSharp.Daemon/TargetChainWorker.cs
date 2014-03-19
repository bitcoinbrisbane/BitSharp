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
    public class TargetChainWorker : Worker
    {
        public event EventHandler<ChainedBlock> OnWinningBlockChanged;

        private readonly IBlockchainRules rules;
        private readonly CacheContext cacheContext;

        private readonly TargetBlockWatcher targetBlockWatcher;
        private ChainedBlocks targetChainedBlocks;

        public TargetChainWorker(IBlockchainRules rules, CacheContext cacheContext, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base("TargetChainWorker", initialNotify, minIdleTime, maxIdleTime)
        {
            this.rules = rules;
            this.cacheContext = cacheContext;

            this.targetBlockWatcher = new TargetBlockWatcher(cacheContext);
            this.targetChainedBlocks = null;

            this.targetBlockWatcher.OnTargetBlockChanged += NotifyWork;
        }

        protected override void SubDispose()
        {
            // cleanup events
            this.targetBlockWatcher.OnTargetBlockChanged -= NotifyWork;

            // cleanup workers
            this.targetBlockWatcher.Dispose();
        }

        public CacheContext CacheContext { get { return this.cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public ChainedBlocks TargetChainedBlocks { get { return this.targetChainedBlocks; } }

        public ChainedBlock WinningBlock
        {
            get
            {
                var targetChainedBlocksLocal = this.targetChainedBlocks;
                if (targetChainedBlocks != null)
                    return this.targetChainedBlocks.LastBlock;
                else
                    return null;
            }
        }

        protected override void WorkAction()
        {
            try
            {
                var targetBlockLocal = this.targetBlockWatcher.TargetBlock;
                var targetChainedBlocksLocal = this.targetChainedBlocks;

                if (targetBlockLocal != null &&
                    (targetChainedBlocksLocal == null || targetBlockLocal.BlockHash != targetChainedBlocksLocal.LastBlock.BlockHash))
                {
                    var newTargetChainedBlocks =
                        targetChainedBlocksLocal != null
                        ? targetChainedBlocksLocal.ToBuilder()
                        : new ChainedBlocksBuilder(ChainedBlocks.CreateForGenesisBlock(this.rules.GenesisChainedBlock));

                    var deltaBlockPath = new MethodTimer(false).Time("deltaBlockPath", () =>
                        new BlockchainWalker().GetBlockchainPath(newTargetChainedBlocks.LastBlock, targetBlockLocal, blockHash => this.CacheContext.ChainedBlockCache[blockHash]));

                    foreach (var rewindBlock in deltaBlockPath.RewindBlocks)
                        newTargetChainedBlocks.RemoveBlock(rewindBlock);
                    foreach (var advanceBlock in deltaBlockPath.AdvanceBlocks)
                        newTargetChainedBlocks.AddBlock(advanceBlock);

                    //Debug.WriteLine("Winning chained block {0} at height {1}, total work: {2}".Format2(targetBlock.BlockHash.ToHexNumberString(), targetBlock.Height, targetBlock.TotalWork.ToString("X")));
                    this.targetChainedBlocks = newTargetChainedBlocks.ToImmutable();

                    var handler = this.OnWinningBlockChanged;
                    if (handler != null)
                        handler(this, targetBlockLocal);
                }
            }
            catch (MissingDataException) { }
        }
    }
}
