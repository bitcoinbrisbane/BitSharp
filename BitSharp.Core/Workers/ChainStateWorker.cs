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

        private readonly Logger logger;
        private readonly Func<Chain> getTargetChain;
        private readonly IKernel kernel;
        private readonly IBlockchainRules rules;
        private readonly BlockCache blockCache;
        private readonly SpentTransactionsCache spentTransactionsCache;
        private readonly InvalidBlockCache invalidBlockCache;

        private readonly ChainStateBuilder chainStateBuilder;
        private Chain currentChain;

        private readonly TimeSpan[] blockTimes;
        private int blockTimesIndex;

        private readonly PruningWorker pruningWorker;

        public ChainStateWorker(ChainStateBuilder chainStateBuilder, Func<Chain> getTargetChain, WorkerConfig workerConfig, Logger logger, IKernel kernel, IBlockchainRules rules, BlockCache blockCache, SpentTransactionsCache spentTransactionsCache, InvalidBlockCache invalidBlockCache)
            : base("ChainStateWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.getTargetChain = getTargetChain;
            this.kernel = kernel;
            this.rules = rules;
            this.blockCache = blockCache;
            this.spentTransactionsCache = spentTransactionsCache;
            this.invalidBlockCache = invalidBlockCache;

            this.blockTimes = new TimeSpan[10000];
            this.blockTimesIndex = -1;

            this.chainStateBuilder = chainStateBuilder;
            this.currentChain = this.chainStateBuilder.Chain.ToImmutable();

            this.pruningWorker = kernel.Get<PruningWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.FromSeconds(30), maxIdleTime: TimeSpan.FromMinutes(5))),
                new ConstructorArgument("getChainStateBuilder", (Func<ChainStateBuilder>)(() => this.chainStateBuilder)));
        }

        public TimeSpan AverageBlockProcessingTime()
        {
            return TimeSpan.FromTicks((long)(this.blockTimes.Where(x => x.Ticks > 0).AverageOrDefault(x => x.Ticks) ?? 0));
        }

        public Chain CurrentChain
        {
            get { return this.currentChain; }
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

                    this.currentChain = this.chainStateBuilder.Chain.ToImmutable();

                    var handler = this.OnChainStateChanged;
                    if (handler != null)
                        handler();
                }

                this.chainStateBuilder.LogBlockchainProgress();
            }
            catch (Exception e)
            {
                if (!(e is MissingDataException))
                {
                    this.logger.WarnException("ChainStateWorker exception", e);
                }

                if (e is ValidationException)
                {
                    var validationException = (ValidationException)e;
                    this.invalidBlockCache[validationException.BlockHash] = validationException.Message;
                }
            }
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