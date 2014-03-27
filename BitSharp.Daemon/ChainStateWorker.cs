using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon
{
    internal class ChainStateWorker : Worker
    {
        public event Action OnChainStateChanged;
        public event Action OnChainStateBuilderChanged;

        private static readonly int MAX_BUILDER_LIFETIME_SECONDS = 60;

        private readonly IBlockchainRules rules;
        private readonly ICacheContext cacheContext;
        private readonly BlockchainCalculator calculator;
        private Func<Chain> getTargetChain;

        private ChainState chainState;
        private readonly ReaderWriterLockSlim chainStateLock;

        private ChainStateBuilder chainStateBuilder;
        private DateTime chainStateBuilderTime;

        public ChainStateWorker(IBlockchainRules rules, ICacheContext cacheContext, Func<Chain> getTargetChain, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base("ChainStateWorker", initialNotify, minIdleTime, maxIdleTime)
        {
            this.rules = rules;
            this.cacheContext = cacheContext;
            this.calculator = new BlockchainCalculator(this.rules, this.cacheContext, this.ShutdownToken.Token);
            this.getTargetChain = getTargetChain;

            this.chainState = ChainState.CreateForGenesisBlock(this.rules.GenesisChainedBlock);
            this.chainStateLock = new ReaderWriterLockSlim();
        }

        public ChainState ChainState { get { return this.chainState; } }

        internal ChainStateBuilder ChainStateBuilder { get { return this.chainStateBuilder; } }

        protected override void SubDispose()
        {
            if (this.chainStateBuilder != null)
            {
                this.chainStateBuilder.Dispose();
                this.chainStateBuilder = null;
            }
        }

        protected override void WorkAction()
        {
            try
            {
                var chainStateLocal = this.chainState;

                if (this.chainStateBuilder != null
                    && this.chainStateBuilder.LastBlockHash != chainStateLocal.LastBlockHash
                    && DateTime.UtcNow - this.chainStateBuilderTime > TimeSpan.FromSeconds(MAX_BUILDER_LIFETIME_SECONDS))
                {
                    var newChain = this.chainStateBuilder.Chain.ToImmutable();
                    var newUtxo = this.chainStateBuilder.Utxo.Close(newChain.LastBlock.BlockHash);

                    this.chainStateBuilder.Dispose();
                    this.chainStateBuilder = null;

                    UpdateCurrentBlockchain(new ChainState(newChain, newUtxo));
                    chainStateLocal = this.chainState;
                }

                if (this.chainStateBuilder == null)
                {
                    this.chainStateBuilderTime = DateTime.UtcNow;
                    this.chainStateBuilder =
                        new ChainStateBuilder
                        (
                            chainStateLocal.Chain.ToBuilder(),
                            new UtxoBuilder(this.cacheContext, chainStateLocal.Utxo)
                        );
                }

                // try to advance the blockchain with the new winning block
                using (var cancelToken = new CancellationTokenSource())
                {
                    var startTime = new Stopwatch();
                    startTime.Start();

                    this.calculator.CalculateBlockchainFromExisting(this.chainStateBuilder, getTargetChain, cancelToken.Token,
                        () =>
                        {
                            var handler = this.OnChainStateBuilderChanged;
                            if (handler != null)
                                handler();

                            if (startTime.Elapsed > TimeSpan.FromSeconds(MAX_BUILDER_LIFETIME_SECONDS))
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
                    Debug.WriteLine(
                        string.Join("\n",
                            new string('-', 200),
                            "ChainStateWorker failure: {0}",
                            "{1}",
                            new string('-', 200)
                        )
                        .Format2(e.Message, e));
                }

                if (this.chainStateBuilder != null && !this.chainStateBuilder.IsConsistent)
                {
                    this.chainStateBuilder.Dispose();
                    this.chainStateBuilder = null;
                }

                if (e is ValidationException)
                {
                    var validationException = (ValidationException)e;
                    this.cacheContext.InvalidBlockCache[validationException.BlockHash] = validationException.Message;
                }

                // try again on failure
                this.NotifyWork();

                var handler = this.OnChainStateChanged;
                if (handler != null)
                    handler();

                var builderHandler = this.OnChainStateBuilderChanged;
                if (builderHandler != null)
                    builderHandler();
            }

            // collect after processing
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
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
