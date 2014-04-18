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
        private readonly BlockCache blockCache;
        private readonly SpentTransactionsCache spentTransactionsCache;
        private readonly InvalidBlockCache invalidBlockCache;

        private ChainState chainState;
        private readonly ReaderWriterLockSlim chainStateLock;

        private ChainStateBuilder chainStateBuilder;
        private DateTime chainStateBuilderTime;

        private readonly TimeSpan[] blockTimes;
        private int blockTimesIndex;

        private readonly PruningWorker pruningWorker;

        public ChainStateWorker(WorkerConfig workerConfig, Func<Chain> getTargetChain, Func<IImmutableSet<IChainStateMonitor>> getMonitors, TimeSpan maxBuilderTime, Logger logger, IKernel kernel, IBlockchainRules rules, BlockCache blockCache, SpentTransactionsCache spentTransactionsCache, InvalidBlockCache invalidBlockCache)
            : base("ChainStateWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.getTargetChain = getTargetChain;
            this.getMonitors = getMonitors;
            this.MaxBuilderTime = maxBuilderTime;
            this.kernel = kernel;
            this.rules = rules;
            this.blockCache = blockCache;
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
            this.pruningWorker.Start();
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
                    var newUtxo = this.chainStateBuilder.ToImmutable(newChain.LastBlock.Hash);
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
                        new ConstructorArgument("getMonitors", this.getMonitors),
                        new ConstructorArgument("chain", chainStateLocal.Chain.ToBuilder()),
                        new ConstructorArgument("parentUtxo", chainStateLocal.Utxo));
                }

                // calculate the new blockchain along the target path
                foreach (var pathElement in ChainedBlockLookAhead(this.chainStateBuilder.Chain.NavigateTowards(this.getTargetChain), lookAhead: 1))
                {
                    // cooperative loop
                    if (!this.IsStarted)
                        break;

                    // get block and metadata for next link in blockchain
                    var direction = pathElement.Item1;
                    var chainedBlock = pathElement.Item2;

                    var stopwatch = new Stopwatch().Started();
                    if (direction > 0)
                    {
                        this.chainStateBuilder.AddBlock(chainedBlock);
                    }
                    else if (direction < 0)
                    {
                        this.chainStateBuilder.RollbackBlock(chainedBlock);
                    }
                    else
                    {
                        Debugger.Break();
                        throw new InvalidOperationException();
                    }
                    stopwatch.Stop();

                    this.blockTimesIndex = (this.blockTimesIndex + 1) % this.blockTimes.Length;
                    this.blockTimes[this.blockTimesIndex] = stopwatch.Elapsed;

                    this.pruningWorker.NotifyWork();

                    var handler = this.OnChainStateBuilderChanged;
                    if (handler != null)
                        handler();

                    if (DateTime.UtcNow - this.chainStateBuilderTime > this.MaxBuilderTime)
                    {
                        this.NotifyWork();
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                if (!(e is MissingDataException))
                {
                    this.logger.WarnException("ChainStateWorker exception", e);
                }

                if (this.chainStateBuilder != null)
                {
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

                //TODO if a chain state was handed off, it should not be disposed until all references to it have been disposed
                if (oldChainState != null)
                    oldChainState.Utxo.Dispose();
            }
            finally
            {
                this.chainStateLock.ExitWriteLock();
            }

            var handler = this.OnChainStateChanged;
            if (handler != null)
                handler();
        }

        private IEnumerable<Tuple<int, ChainedBlock>> ChainedBlockLookAhead(IEnumerable<Tuple<int, ChainedHeader>> chain, int lookAhead)
        {
            return chain
                .Select(
                    chainedHeaderTuple =>
                    {
                        try
                        {
                            // cooperative loop
                            if (!this.IsStarted)
                                throw new OperationCanceledException();

                            var direction = chainedHeaderTuple.Item1;

                            var chainedHeader = chainedHeaderTuple.Item2;
                            var block = new MethodTimer(false).Time("GetBlock", () =>
                                this.blockCache[chainedHeader.Hash]);

                            var chainedBlock = new ChainedBlock(chainedHeader, block);

                            return Tuple.Create(direction, chainedBlock);
                        }
                        catch (MissingDataException e)
                        {
                            this.logger.Info("Stalled, MissingDataException: {0}".Format2(e.Key));
                            throw;
                        }
                    })
                .LookAhead(lookAhead);
        }
    }
}