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
        private readonly IBlockchainRules rules;
        private readonly CoreStorage coreStorage;

        private readonly DurationMeasure blockProcessingDurationMeasure;
        private readonly CountMeasure blockMissCountMeasure;
        private UInt256? lastBlockMissHash;

        private readonly TargetChainWorker targetChainWorker;
        private readonly ChainStateBuilder chainStateBuilder;
        private Chain currentChain;

        public ChainStateWorker(WorkerConfig workerConfig, TargetChainWorker targetChainWorker, ChainStateBuilder chainStateBuilder, Func<Chain> getTargetChain, Logger logger, IBlockchainRules rules, CoreStorage coreStorage)
            : base("ChainStateWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.getTargetChain = getTargetChain;
            this.rules = rules;
            this.coreStorage = coreStorage;

            this.blockProcessingDurationMeasure = new DurationMeasure(sampleCutoff: TimeSpan.FromMinutes(5));
            this.blockMissCountMeasure = new CountMeasure(TimeSpan.FromSeconds(30));

            this.targetChainWorker = targetChainWorker;
            this.chainStateBuilder = chainStateBuilder;
            this.currentChain = this.chainStateBuilder.Chain;
        }

        public event Action<UInt256> BlockMissed;

        public TimeSpan AverageBlockProcessingTime()
        {
            return this.blockProcessingDurationMeasure.GetAverage();
        }

        public int GetBlockMissCount()
        {
            return this.blockMissCountMeasure.GetCount();
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
                this.blockMissCountMeasure,
            }.DisposeList();
        }

        protected override void WorkAction()
        {
            try
            {
                // calculate the new blockchain along the target path
                var didWork = false;
                foreach (var pathElement in this.chainStateBuilder.Chain.NavigateTowards(this.getTargetChain))
                {
                    // cooperative loop
                    this.ThrowIfCancelled();

                    didWork = true;

                    // get block and metadata for next link in blockchain
                    var direction = pathElement.Item1;
                    var chainedHeader = pathElement.Item2;
                    var blockTxes = this.coreStorage.ReadBlockTransactions(chainedHeader.Hash, chainedHeader.MerkleRoot).LookAhead(100);

                    var blockStopwatch = Stopwatch.StartNew();
                    if (direction > 0)
                    {
                        this.chainStateBuilder.AddBlock(chainedHeader, blockTxes);
                    }
                    else if (direction < 0)
                    {
                        this.chainStateBuilder.RollbackBlock(chainedHeader, blockTxes);
                    }
                    else
                    {
                        Debugger.Break();
                        throw new InvalidOperationException();
                    }
                    blockStopwatch.Stop();
                    this.blockProcessingDurationMeasure.Tick(blockStopwatch.Elapsed);

                    this.currentChain = this.chainStateBuilder.Chain;

                    var handler = this.OnChainStateChanged;
                    if (handler != null)
                        handler();
                }

                if (didWork)
                    this.chainStateBuilder.LogBlockchainProgress();
            }
            catch (OperationCanceledException) { }
            catch (AggregateException e)
            {
                foreach (var innerException in e.InnerExceptions)
                {
                    HandleException(innerException);
                }
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        private void HandleException(Exception e)
        {
            var missingException = e as MissingDataException;
            if (missingException != null)
            {
                var missingBlockHash = (UInt256)missingException.Key;
                if (this.lastBlockMissHash == null || this.lastBlockMissHash.Value != missingBlockHash)
                {
                    this.lastBlockMissHash = missingBlockHash;
                    this.blockMissCountMeasure.Tick();
                }
                
                RaiseBlockMissed(missingBlockHash);
            }
            else
            {
                this.logger.WarnException("ChainStateWorker exception.", e);

                var validationException = e as ValidationException;
                if (validationException != null)
                {
                    // mark block as invalid
                    this.coreStorage.MarkBlockInvalid(validationException.BlockHash);

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
                            var blockTxes = this.coreStorage.ReadBlockTransactions(chainedHeader.Hash, chainedHeader.MerkleRoot).LookAhead(txLookAhead);

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

        private void RaiseBlockMissed(UInt256 blockHash)
        {
            var handler = this.BlockMissed;
            if (handler != null)
                handler(blockHash);
        }
    }
}