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
        public event Action OnTargetBlockChanged;
        public event Action OnTargetChainChanged;

        private readonly IBlockchainRules rules;
        private readonly ICacheContext cacheContext;

        private readonly TargetBlockWorker targetBlockWorker;
        private Chain targetChain;

        private readonly AutoResetEvent rescanEvent;

        public TargetChainWorker(IBlockchainRules rules, ICacheContext cacheContext, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base("TargetChainWorker", initialNotify, minIdleTime, maxIdleTime)
        {
            this.rules = rules;
            this.cacheContext = cacheContext;

            this.rescanEvent = new AutoResetEvent(false);

            this.targetBlockWorker = new TargetBlockWorker(cacheContext, initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue);

            this.targetBlockWorker.OnTargetBlockChanged += HandleTargetBlockChanged;
            this.cacheContext.ChainedBlockCache.OnAddition += HandleChainedBlock;
            this.cacheContext.InvalidBlockCache.OnAddition += HandleInvalidBlock;
        }

        protected override void SubDispose()
        {
            // cleanup events
            this.targetBlockWorker.OnTargetBlockChanged -= HandleTargetBlockChanged;
            this.cacheContext.ChainedBlockCache.OnAddition -= HandleChainedBlock;
            this.cacheContext.InvalidBlockCache.OnAddition -= HandleInvalidBlock;

            // cleanup workers
            this.targetBlockWorker.Dispose();
        }

        public ICacheContext CacheContext { get { return this.cacheContext; } }

        public Chain TargetChain { get { return this.targetChain; } }

        public ChainedBlock TargetBlock { get { return this.targetBlockWorker.TargetBlock; } }

        internal TargetBlockWorker TargetBlockWorker { get { return this.targetBlockWorker; } }

        protected override void SubStart()
        {
            this.targetBlockWorker.Start();
        }

        protected override void SubStop()
        {
            this.targetBlockWorker.Stop();
        }

        protected override void WorkAction()
        {
            try
            {
                if (this.rescanEvent.WaitOne(0))
                {
                    this.targetChain = null;
                }

                var targetBlockLocal = this.targetBlockWorker.TargetBlock;
                var targetChainLocal = this.targetChain;

                if (targetBlockLocal != null &&
                    (targetChainLocal == null || targetBlockLocal.BlockHash != targetChainLocal.LastBlock.BlockHash))
                {
                    var newTargetChain =
                        targetChainLocal != null
                        ? targetChainLocal.ToBuilder()
                        : new ChainBuilder(Chain.CreateForGenesisBlock(this.rules.GenesisChainedBlock));

                    var deltaBlockPath = new MethodTimer(false).Time("deltaBlockPath", () =>
                        new BlockchainWalker().GetBlockchainPath(newTargetChain.LastBlock, targetBlockLocal, blockHash => this.CacheContext.ChainedBlockCache[blockHash]));

                    foreach (var rewindBlock in deltaBlockPath.RewindBlocks)
                    {
                        if (this.cacheContext.InvalidBlockCache.ContainsKey(rewindBlock.BlockHash))
                        {
                            this.rescanEvent.Set();
                            return;
                        }

                        newTargetChain.RemoveBlock(rewindBlock);
                    }

                    var invalid = false;
                    foreach (var advanceBlock in deltaBlockPath.AdvanceBlocks)
                    {
                        if (this.cacheContext.InvalidBlockCache.ContainsKey(advanceBlock.BlockHash))
                            invalid = true;

                        if (!invalid)
                            newTargetChain.AddBlock(advanceBlock);
                        else
                            this.cacheContext.InvalidBlockCache.TryAdd(advanceBlock.BlockHash, "");
                    }

                    //Debug.WriteLine("Winning chained block {0} at height {1}, total work: {2}".Format2(targetBlock.BlockHash.ToHexNumberString(), targetBlock.Height, targetBlock.TotalWork.ToString("X")));
                    this.targetChain = newTargetChain.ToImmutable();

                    var handler = this.OnTargetChainChanged;
                    if (handler != null)
                        handler();
                }
            }
            catch (MissingDataException) { }
        }

        private void HandleTargetBlockChanged()
        {
            this.NotifyWork();
            
            var handler = this.OnTargetBlockChanged;
            if (handler != null)
                handler();
        }

        private void HandleChainedBlock(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            this.NotifyWork();
        }

        private void HandleInvalidBlock(UInt256 blockHash, string data)
        {
            this.rescanEvent.Set();
            this.NotifyWork();
        }
    }
}
