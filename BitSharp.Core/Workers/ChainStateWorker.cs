using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Builders;
using BitSharp.Core.Wallet;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using Ninject;
using Ninject.Parameters;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Core.Monitor;

namespace BitSharp.Core.Workers
{
    internal class ChainStateWorker : Worker
    {
        public event Action OnChainStateChanged;
        public event Action OnChainStateBuilderChanged;

        private readonly Logger logger;
        private readonly Func<Chain> getTargetChain;
        private readonly Func<IImmutableSet<IChainStateMonitor>> getMonitors;
        private readonly IKernel kernel;
        private readonly IBlockchainRules rules;
        private readonly TransactionCache transactionCache;
        private readonly SpentTransactionsCache spentTransactionsCache;
        private readonly InvalidBlockCache invalidBlockCache;

        private ChainState chainState;
        private readonly ReaderWriterLockSlim chainStateLock;

        private ChainStateBuilder chainStateBuilder;
        private DateTime chainStateBuilderTime;

        private readonly TimeSpan[] blockTimes;
        private int blockTimesIndex;

        private readonly PruningWorker pruningWorker;

        public ChainStateWorker(WorkerConfig workerConfig, Func<Chain> getTargetChain, Func<IImmutableSet<IChainStateMonitor>> getMonitors, TimeSpan maxBuilderTime, Logger logger, IKernel kernel, IBlockchainRules rules, TransactionCache transactionCache, SpentTransactionsCache spentTransactionsCache, InvalidBlockCache invalidBlockCache)
            : base("ChainStateWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.getTargetChain = getTargetChain;
            this.getMonitors = getMonitors;
            this.MaxBuilderTime = maxBuilderTime;
            this.kernel = kernel;
            this.rules = rules;
            this.transactionCache = transactionCache;
            this.spentTransactionsCache = spentTransactionsCache;
            this.invalidBlockCache = invalidBlockCache;

            this.chainState = ChainState.CreateForGenesisBlock(this.rules.GenesisChainedHeader);
            this.chainStateLock = new ReaderWriterLockSlim();

            this.blockTimes = new TimeSpan[10000];
            this.blockTimesIndex = -1;

            this.pruningWorker = kernel.Get<PruningWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.FromSeconds(30), maxIdleTime: TimeSpan.FromMinutes(5))),
                new ConstructorArgument("getChainState", (Func<ChainState>)(() => this.chainState)),
                new ConstructorArgument("getChainStateBuilder", (Func<ChainStateBuilder>)(() => this.chainStateBuilder)));
        }

        public TimeSpan MaxBuilderTime { get; set; }

        public ChainState ChainState { get { return this.chainState; } }

        internal ChainStateBuilder ChainStateBuilder { get { return this.chainStateBuilder; } }

        public TimeSpan AverageBlockProcessingTime()
        {
            return TimeSpan.FromTicks((long)(this.blockTimes.Where(x => x.Ticks > 0).AverageOrDefault(x => x.Ticks) ?? 0));
        }

        protected override void SubDispose()
        {
            new IDisposable[]
            {
                this.chainStateBuilder,
                this.pruningWorker,
            }.DisposeList();
        }

        protected override void SubStart()
        {
            //this.pruningWorker.Start();
        }

        protected override void SubStop()
        {
            this.pruningWorker.Stop();
        }

        protected override void WorkAction()
        {
            try
            {
                var chainStateLocal = this.chainState;

                if (this.chainStateBuilder != null
                    && this.chainStateBuilder.LastBlockHash != chainStateLocal.LastBlockHash
                    && DateTime.UtcNow - this.chainStateBuilderTime > this.MaxBuilderTime)
                {
                    this.chainStateBuilder.LogBlockchainProgress();

                    // ensure rollback information is fully saved, back to pruning limit, before considering new chain state committed
                    var blocksPerDay = 144;
                    var pruneBuffer = blocksPerDay * 7;
                    foreach (var block in this.chainStateBuilder.Chain.Blocks.Reverse().Take(pruneBuffer))
                    {
                        if (block.Height > 0 && !this.spentTransactionsCache.ContainsKey(block.Hash))
                            throw new InvalidOperationException();
                    }
                    this.spentTransactionsCache.Flush();

                    //TODO keep the builder open favors performance when catching up
                    //TODO once caught up, it should switch over to quickly returning committed utxo's as new blocks come in
                    //TODO should be configurable, as doing this requires keeping two copies of the utxo on disk at all times
                    var newChain = this.chainStateBuilder.Chain.ToImmutable();
                    var newUtxo = this.chainStateBuilder.Utxo.ToImmutable(newChain.LastBlock.Hash);
                    //this.chainStateBuilder.Dispose();
                    //this.chainStateBuilder = null;
                    this.chainStateBuilderTime = DateTime.UtcNow;

                    UpdateCurrentBlockchain(new ChainState(newChain, newUtxo));
                    chainStateLocal = this.chainState;

                    this.pruningWorker.ForceWorkAndWait();
                }

                if (this.chainStateBuilder == null)
                {
                    this.chainStateBuilderTime = DateTime.UtcNow;
                    this.chainStateBuilder =
                        this.kernel.Get<ChainStateBuilder>(
                        new ConstructorArgument("chain", chainStateLocal.Chain.ToBuilder()),
                        new ConstructorArgument("utxo", new UtxoBuilder(chainStateLocal.Utxo, this.logger, this.kernel, this.transactionCache)),
                        new ConstructorArgument("shutdownToken", this.ShutdownToken.Token));
                }

                // try to advance the blockchain with the new winning block
                using (var cancelToken = new CancellationTokenSource())
                {
                    this.chainStateBuilder.CalculateBlockchainFromExisting(this.getTargetChain, this.getMonitors, cancelToken.Token,
                        (blockTime) =>
                        {
                            if (this.ShutdownToken.IsCancellationRequested)
                            {
                                cancelToken.Cancel();
                                return;
                            }

                            this.blockTimesIndex = (this.blockTimesIndex + 1) % this.blockTimes.Length;
                            this.blockTimes[this.blockTimesIndex] = blockTime;

                            this.pruningWorker.NotifyWork();

                            var handler = this.OnChainStateBuilderChanged;
                            if (handler != null)
                                handler();

                            if (DateTime.UtcNow - this.chainStateBuilderTime > this.MaxBuilderTime)
                            {
                                this.NotifyWork();
                                cancelToken.Cancel();
                            }
                        });
                }
            }
            catch (Exception e)
            {
                if (!(e is MissingDataException))
                {
                    this.logger.WarnException("ChainStateWorker exception", e);
                }

                if (this.chainStateBuilder != null && !this.chainStateBuilder.IsConsistent)
                {
                    this.chainStateBuilder.Dispose();
                    this.chainStateBuilder = null;

                    var builderHandler = this.OnChainStateBuilderChanged;
                    if (builderHandler != null)
                        builderHandler();
                }

                if (e is ValidationException)
                {
                    var validationException = (ValidationException)e;
                    this.invalidBlockCache[validationException.BlockHash] = validationException.Message;
                }
            }
        }

        private void UpdateCurrentBlockchain(ChainState newChainState)
        {
            this.chainStateLock.EnterWriteLock();
            try
            {
                var oldChainState = this.chainState;

                this.chainState = newChainState;

                //TODO stop gap
                if (oldChainState != null)
                    oldChainState.Utxo.DisposeDelete();
            }
            finally
            {
                this.chainStateLock.ExitWriteLock();
            }

            var handler = this.OnChainStateChanged;
            if (handler != null)
                handler();
        }
    }
}