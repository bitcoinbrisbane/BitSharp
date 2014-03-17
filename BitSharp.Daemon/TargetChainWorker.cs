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
    public class TargetChainWorker : IDisposable
    {
        public event EventHandler<ChainedBlock> OnWinningBlockChanged;

        private readonly CancellationTokenSource shutdownToken;

        private readonly IBlockchainRules rules;
        private readonly CacheContext cacheContext;

        private ChainedBlock targetBlock;
        private readonly ReaderWriterLockSlim targetBlockLock;

        private ChainedBlocks targetChainedBlocks;

        private readonly Worker readTargetChainedBlocksWorker;

        public TargetChainWorker(IBlockchainRules rules, CacheContext cacheContext)
        {
            this.shutdownToken = new CancellationTokenSource();

            this.rules = rules;
            this.cacheContext = cacheContext;

            this.targetBlock = this.rules.GenesisChainedBlock;
            this.targetBlockLock = new ReaderWriterLockSlim();

            this.targetChainedBlocks = ChainedBlocks.CreateForGenesisBlock(this.rules.GenesisChainedBlock);

            // wire up cache events
            this.cacheContext.ChainedBlockCache.OnAddition += CheckChainedBlock;
            this.cacheContext.ChainedBlockCache.OnModification += CheckChainedBlock;

            // create workers
            this.readTargetChainedBlocksWorker = new Worker("TargetChainWorker.ReadTargetChainedBlocksWorker", ReadTargetChainedBlocksWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30));

            //TODO periodic rescan
            //new Thread(() =>
            //    {
                    new MethodTimer().Time("SelectMaxTotalWorkBlocks", () =>
                    {
                        foreach (var chainedBlock in this.cacheContext.StorageContext.ChainedBlockStorage.SelectMaxTotalWorkBlocks())
                        {
                            // cooperative loop
                            if (this.shutdownToken.IsCancellationRequested)
                                return;

                            CheckChainedBlock(chainedBlock.BlockHash, chainedBlock);
                        }
                    });

                    //Debugger.Break();
                //}).start();
        }

        public CacheContext CacheContext { get { return this.cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public ChainedBlocks TargetChainedBlocks { get { return this.targetChainedBlocks; } }

        public ChainedBlock WinningBlock { get { return this.targetChainedBlocks.LastBlock; } }

        public void Start()
        {
            try
            {
                // start loading the existing state from storage
                //TODO LoadExistingState();

                // startup workers
                this.readTargetChainedBlocksWorker.Start();
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
            this.CacheContext.ChainedBlockCache.OnAddition -= CheckChainedBlock;
            this.CacheContext.ChainedBlockCache.OnModification -= CheckChainedBlock;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
                this.readTargetChainedBlocksWorker,
                this.shutdownToken
            }.DisposeList();
        }

        private void CheckChainedBlock(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            try
            {
                if (chainedBlock == null)
                    chainedBlock = this.CacheContext.ChainedBlockCache[blockHash];
            }
            catch (MissingDataException) { return; }

            this.targetBlockLock.DoWrite(() =>
            {
                if (this.targetBlock == null
                    || chainedBlock.TotalWork > this.targetBlock.TotalWork)
                {
                    this.targetBlock = chainedBlock;
                    this.readTargetChainedBlocksWorker.NotifyWork();
                }
            });
        }

        private void ReadTargetChainedBlocksWorker()
        {
            try
            {
                var targetBlockLocal = this.targetBlock;
                var targetChainedBlocksLocal = this.targetChainedBlocks;

                if (targetBlockLocal.BlockHash != targetChainedBlocksLocal.LastBlock.BlockHash)
                {
                    var newTargetChainedBlocks = targetChainedBlocksLocal.ToBuilder();

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
