using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
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
using System.IO;
using Ninject;
using Ninject.Parameters;
using NLog;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Core.Workers;
using BitSharp.Core.Rules;
using System.Security.Cryptography;
using BitSharp.Core.Monitor;
using BitSharp.Core.Builders;

namespace BitSharp.Core
{
    //TODO have a class for building blockchain, and a class for cleaning?

    // blockchain rules here:
    //
    // https://github.com/bitcoin/bitcoin/blob/4ad73c6b080c46808b0c53b62ab6e4074e48dc75/src/main.cpp
    //
    // bool ConnectBlock(CBlock& block, CValidationState& state, CBlockIndex* pindex, CCoinsViewCache& view, bool fJustCheck)
    // https://github.com/bitcoin/bitcoin/blob/4ad73c6b080c46808b0c53b62ab6e4074e48dc75/src/main.cpp#L1734
    //
    //TODO BIP-030

    //TODO compact UTXO's and other immutables in the blockchains on a thread
    public class CoreDaemon : IDisposable
    {
        public event EventHandler OnTargetChainChanged;
        public event EventHandler OnChainStateChanged;
        public event Action<UInt256> BlockMissed;

        private readonly Logger logger;
        private readonly IKernel kernel;
        private readonly IBlockchainRules rules;
        private readonly IStorageManager storageManager;
        private readonly CoreStorage coreStorage;

        private readonly CancellationTokenSource shutdownToken;

        private readonly ChainStateBuilder chainStateBuilder;

        private readonly TargetChainWorker targetChainWorker;
        private readonly ChainStateWorker chainStateWorker;
        private readonly PruningWorker pruningWorker;
        private readonly DefragWorker defragWorker;
        private readonly WorkerMethod gcWorker;
        private readonly WorkerMethod utxoScanWorker;

        public CoreDaemon(Logger logger, IKernel kernel, IBlockchainRules rules, IStorageManager storageManager)
        {
            this.logger = logger;
            this.shutdownToken = new CancellationTokenSource();

            this.kernel = kernel;
            this.rules = rules;
            this.storageManager = storageManager;
            this.coreStorage = new CoreStorage(storageManager, logger);

            // write genesis block out to storage
            this.coreStorage.AddGenesisBlock(this.rules.GenesisChainedHeader);
            this.coreStorage.TryAddBlock(this.rules.GenesisBlock);

            // create chain state builder
            this.chainStateBuilder = new ChainStateBuilder(this.logger, this.rules, this.coreStorage);

            // add genesis block to chain state, if needed
            if (this.chainStateBuilder.Chain.Height < 0)
                this.chainStateBuilder.AddBlock(this.rules.GenesisChainedHeader, this.rules.GenesisBlock.Transactions);

            // create workers
            this.targetChainWorker = new TargetChainWorker(
                new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromMilliseconds(50), maxIdleTime: TimeSpan.FromSeconds(30)),
                this.logger, this.rules, this.coreStorage);

            this.chainStateWorker = new ChainStateWorker(
                new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromMilliseconds(50), maxIdleTime: TimeSpan.FromSeconds(5)),
                this.targetChainWorker, this.chainStateBuilder, this.logger, this.rules, this.coreStorage);

            this.pruningWorker = new PruningWorker(
                new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromSeconds(60), maxIdleTime: TimeSpan.FromMinutes(15)),
                this.coreStorage, this.chainStateWorker, this.chainStateBuilder, this.logger, this.rules);

            this.defragWorker = new DefragWorker(
                new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromMinutes(5), maxIdleTime: TimeSpan.FromMinutes(5)),
                this.coreStorage, this.logger);

            // notify defrag worker after pruning
            this.pruningWorker.OnWorkFinished += this.defragWorker.NotifyWork;

            this.chainStateWorker.BlockMissed += HandleBlockMissed;

            this.targetChainWorker.OnTargetChainChanged +=
                () =>
                {
                    var handler = this.OnTargetChainChanged;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                };

            this.chainStateWorker.OnChainStateChanged +=
                () =>
                {
                    this.pruningWorker.NotifyWork();
                    this.utxoScanWorker.NotifyWork();

                    var handler = this.OnChainStateChanged;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                };

            this.gcWorker = new WorkerMethod("GC Worker",
                _ =>
                {
                    this.logger.Info(
                        string.Join("\n",
                            new string('-', 80),
                            "GC Memory:      {0,10:#,##0.00} MB",
                            "Process Memory: {1,10:#,##0.00} MB",
                            new string('-', 80)
                        )
                        .Format2
                        (
                        /*0*/ (float)GC.GetTotalMemory(false) / 1.MILLION(),
                        /*1*/ (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()
                        ));
                }, initialNotify: true, minIdleTime: TimeSpan.FromSeconds(30), maxIdleTime: TimeSpan.FromSeconds(30), logger: this.logger);

            this.utxoScanWorker = new WorkerMethod("UTXO Scan Worker",
                _ =>
                {
                    // time taking chain state snapshots
                    var stopwatch = Stopwatch.StartNew();
                    int chainStateHeight;
                    using (var chainState = this.GetChainState())
                    {
                        chainStateHeight = chainState.Chain.Height;
                    }
                    stopwatch.Stop();
                    this.logger.Info("GetChainState at {0:#,##0}: {1:#,##0.00}s".Format2(chainStateHeight, stopwatch.Elapsed.TotalSeconds));

                    // time enumerating chain state snapshots
                    stopwatch = Stopwatch.StartNew();
                    using (var chainState = this.GetChainState())
                    {
                        chainStateHeight = chainState.Chain.Height;
                        chainState.ReadUnspentTransactions().Count();
                    }
                    stopwatch.Stop();
                    this.logger.Info("Enumerate chain state at {0:#,##0}: {1:#,##0.00}s".Format2(chainStateHeight, stopwatch.Elapsed.TotalSeconds));

                    //using (var chainStateLocal = this.GetChainState())
                    //{
                    //    new MethodTimer(this.logger).Time("UTXO Commitment: {0:#,##0}".Format2(chainStateLocal.UnspentTxCount), () =>
                    //    {
                    //        using (var utxoStream = new UtxoStream(this.logger, chainStateLocal.ReadUnspentTransactions()))
                    //        {
                    //            var sha256 = new SHA256Managed();
                    //            var utxoHash = sha256.ComputeHash(utxoStream);
                    //            this.logger.Info("UXO Commitment Hash: {0}".Format2(utxoHash.ToHexNumberString()));
                    //        }
                    //    });

                    //    //new MethodTimer().Time("Full UTXO Scan: {0:#,##0}".Format2(chainStateLocal.Utxo.TransactionCount), () =>
                    //    //{
                    //    //    var sha256 = new SHA256Managed();
                    //    //    foreach (var output in chainStateLocal.Utxo.GetUnspentTransactions())
                    //    //    {
                    //    //    }
                    //    //});
                    //}
                }, initialNotify: true, minIdleTime: TimeSpan.FromSeconds(60), maxIdleTime: TimeSpan.FromSeconds(60), logger: this.logger);
        }

        public void Dispose()
        {
            this.Stop();

            // cleanup events
            this.pruningWorker.OnWorkFinished -= this.defragWorker.NotifyWork;
            this.chainStateWorker.BlockMissed -= HandleBlockMissed;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
                this.defragWorker,
                this.pruningWorker,
                this.chainStateWorker,
                this.targetChainWorker,
                this.gcWorker,
                this.utxoScanWorker,
                this.chainStateBuilder,
                this.shutdownToken
            }.DisposeList();
        }

        public CoreStorage CoreStorage { get { return this.coreStorage; } }

        public Chain TargetChain { get { return this.targetChainWorker.TargetChain; } }

        public int TargetChainHeight
        {
            get
            {
                var targetChainLocal = this.targetChainWorker.TargetChain;
                if (targetChainLocal != null)
                    return targetChainLocal.Height;
                else
                    return -1;
            }
        }

        public Chain CurrentChain
        {
            get { return this.chainStateWorker.CurrentChain; }
        }

        public float GetBlockRate(TimeSpan perUnitTime)
        {
            return this.chainStateBuilder.Stats.blockRateMeasure.GetAverage(perUnitTime);
        }

        public float GetTxRate(TimeSpan perUnitTime)
        {
            return this.chainStateBuilder.Stats.txRateMeasure.GetAverage(perUnitTime);
        }

        public float GetInputRate(TimeSpan perUnitTime)
        {
            return this.chainStateBuilder.Stats.inputRateMeasure.GetAverage(perUnitTime);
        }

        public TimeSpan AverageBlockProcessingTime()
        {
            return this.chainStateWorker.AverageBlockProcessingTime();
        }

        public int GetBlockMissCount()
        {
            return this.chainStateWorker.GetBlockMissCount();
        }

        public void Start()
        {
            // startup workers
            this.targetChainWorker.Start();
            this.chainStateWorker.Start();
            //this.pruningWorker.Start();
            this.defragWorker.Start();
            this.gcWorker.Start();
            //this.utxoScanWorker.Start();
        }

        public void Stop()
        {
            // stop workers
            this.utxoScanWorker.Stop();
            this.pruningWorker.Stop();
            this.chainStateWorker.Stop();
            this.targetChainWorker.Stop();
            this.defragWorker.Stop();
            this.gcWorker.Stop();
        }

        public void WaitForUpdate()
        {
            this.targetChainWorker.WaitForUpdate();
            this.chainStateWorker.WaitForUpdate();
        }

        //TODO need to implement functionality to prevent pruning from removing block data that is being used by chain state snapshots
        //TODO i.e. don't prune past height X
        public IChainState GetChainState()
        {
            return this.chainStateBuilder.ToImmutable();
        }

        public IDisposable SubscribeChainStateVisitor(IChainStateVisitor visitor)
        {
            throw new NotImplementedException();
        }

        private void HandleBlockMissed(UInt256 blockHash)
        {
            var handler = this.BlockMissed;
            if (handler != null)
                handler(blockHash);
        }

        private void ValidateCurrentChainWorker()
        {
            throw new NotImplementedException();

            //var chainStateLocal = this.chainState;
            //if (chainStateLocal != null && this.Rules.GenesisBlock != null)
            //{
            //    var stopwatch = Stopwatch.StartNew();

            //    // revalidate current blockchain
            //    try
            //    {
            //        Calculator.RevalidateBlockchain(chainStateLocal.CurrentBlock, this.Rules.GenesisBlock);
            //    }
            //    catch (ValidationException)
            //    {
            //        //TODO this does not cancel a blockchain that is currently being processed

            //        Debug.WriteLine("******************************");
            //        Debug.WriteLine("******************************");
            //        Debug.WriteLine("BLOCKCHAIN ERROR DETECTED, ROLLING BACK TO GENESIS");
            //        Debug.WriteLine("******************************");
            //        Debug.WriteLine("******************************");

            //        UpdateChainState(new ChainState(this._rules.GenesisBlockchain));
            //    }
            //    catch (MissingDataException e)
            //    {
            //        HandleMissingData(e);
            //    }

            //    stopwatch.Stop();
            //    Debug.WriteLine("ValidateCurrentChainWorker: {0:#,##0.000}s".Format2(stopwatch.ElapsedSecondsFloat()));
            //}
        }

        private void WriteBlockchainWorker()
        {
            throw new NotImplementedException();

            //var stopwatch = Stopwatch.StartNew();

            //// grab a snapshot
            //var chainStateLocal = this.chainState;

            //// don't write out genesis blockchain
            //if (chainStateLocal.CurrentBlock.Height > 0)
            //{
            //    //TODO
            //    this.StorageContext.BlockchainStorage.WriteBlockchain(chainStateLocal.CurrentBlock);
            //    this.StorageContext.BlockchainStorage.RemoveBlockchains(chainStateLocal.CurrentBlock.TotalWork);
            //}

            //stopwatch.Stop();
            //Debug.WriteLine("WriteBlockchainWorker: {0:#,##0.000}s".Format2(stopwatch.ElapsedSecondsFloat()));
        }
    }
}