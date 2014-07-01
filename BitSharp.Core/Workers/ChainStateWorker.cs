using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Builders;
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
        private readonly IBlockStorageNew blockCache;
        private readonly SpentTransactionsCache spentTransactionsCache;
        private readonly InvalidBlockCache invalidBlockCache;

        private readonly DurationMeasure blockProcessingDurationMeasure;
        private readonly TargetChainWorker targetChainWorker;
        private readonly ChainStateBuilder chainStateBuilder;
        private Chain currentChain;

        private readonly PruningWorker pruningWorker;

        public ChainStateWorker(TargetChainWorker targetChainWorker, ChainStateBuilder chainStateBuilder, Func<Chain> getTargetChain, WorkerConfig workerConfig, Logger logger, IKernel kernel, IBlockchainRules rules, IBlockStorageNew blockCache, SpentTransactionsCache spentTransactionsCache, InvalidBlockCache invalidBlockCache)
            : base("ChainStateWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.getTargetChain = getTargetChain;
            this.kernel = kernel;
            this.rules = rules;
            this.blockCache = blockCache;
            this.spentTransactionsCache = spentTransactionsCache;
            this.invalidBlockCache = invalidBlockCache;

            this.blockProcessingDurationMeasure = new DurationMeasure();

            this.targetChainWorker = targetChainWorker;
            this.chainStateBuilder = chainStateBuilder;
            this.currentChain = this.chainStateBuilder.Chain.ToImmutable();

            this.pruningWorker = kernel.Get<PruningWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.FromSeconds(30), maxIdleTime: TimeSpan.FromMinutes(5))),
                new ConstructorArgument("getChainStateBuilder", (Func<ChainStateBuilder>)(() => this.chainStateBuilder)));
        }

        public TimeSpan AverageBlockProcessingTime()
        {
            return this.blockProcessingDurationMeasure.GetAverage();
        }

        public Chain CurrentChain
        {
            get { return this.currentChain; }
        }

        protected override void SubDispose()
        {
            new IDisposable[]
            {
                this.blockProcessingDurationMeasure,
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
                // calculate the new blockchain along the target path
                var didWork = false;
                foreach (var pathElement in
                    this.ChainedBlockLookAhead(this.chainStateBuilder.Chain.NavigateTowards(this.getTargetChain),
                        chainLookAhead: 10, txLookAhead: 100))
                {
                    // cooperative loop
                    this.ThrowIfCancelled();

                    didWork = true;

                    // get block and metadata for next link in blockchain
                    var direction = pathElement.Item1;
                    var chainedHeader = pathElement.Item2;
                    var blockTxes = pathElement.Item3;

                    var blockStopwatch = Stopwatch.StartNew();
                    if (direction > 0)
                    {
                        this.chainStateBuilder.AddBlock(chainedHeader, blockTxes);
                    }
                    else if (direction < 0)
                    {
                        //TODO
                        //this.chainStateBuilder.RollbackBlock(chainedBlock);
                    }
                    else
                    {
                        Debugger.Break();
                        throw new InvalidOperationException();
                    }
                    blockStopwatch.Stop();
                    this.blockProcessingDurationMeasure.Tick(blockStopwatch.Elapsed);

                    this.pruningWorker.NotifyWork();

                    this.currentChain = this.chainStateBuilder.Chain.ToImmutable();

                    var handler = this.OnChainStateChanged;
                    if (handler != null)
                        handler();
                }

                if (didWork)
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

                    // immediately update the target chain if there is a validation error
                    this.targetChainWorker.ForceWorkAndWait();
                }
            }
        }

        private IEnumerable<Tuple<int, ChainedHeader, IEnumerable<BlockTx>>> ChainedBlockLookAhead(IEnumerable<Tuple<int, ChainedHeader>> chain, int chainLookAhead, int txLookAhead)
        {
            return chain
                .Select(
                    chainedHeaderTuple =>
                    {
                        try
                        {
                            // cooperative loop
                            this.ThrowIfCancelled();

                            var direction = chainedHeaderTuple.Item1;
                            var chainedHeader = chainedHeaderTuple.Item2;
                            var blockTxes = this.blockCache.ReadBlock(chainedHeader).LookAhead(txLookAhead);

                            return Tuple.Create(direction, chainedHeader, blockTxes);
                        }
                        catch (MissingDataException e)
                        {
                            this.logger.Debug("Stalled, MissingDataException: {0}".Format2(e.Key));
                            throw;
                        }
                    })
                .LookAhead(chainLookAhead);
        }
    }
}