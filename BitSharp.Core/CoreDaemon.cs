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
using BitSharp.Domain;

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
        public event EventHandler OnTargetBlockChanged;
        public event EventHandler OnTargetChainChanged;
        public event EventHandler OnChainStateChanged;

        private readonly Logger logger;
        private readonly IKernel kernel;
        private readonly IBlockchainRules rules;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly ChainedHeaderCache chainedHeaderCache;
        private readonly IBlockStorageNew blockCache;

        private readonly CancellationTokenSource shutdownToken;

        private readonly ChainStateBuilder chainStateBuilder;
        private ChainState prevChainState;
        private ChainState chainState;
        private readonly ReaderWriterLockSlim chainStateLock;

        private readonly ChainingWorker chainingWorker;
        private readonly TargetChainWorker targetChainWorker;
        private readonly ChainStateWorker chainStateWorker;
        private readonly WorkerMethod gcWorker;
        private readonly WorkerMethod utxoScanWorker;

        public CoreDaemon(Logger logger, IKernel kernel, IBlockchainRules rules, BlockHeaderCache blockHeaderCache, ChainedHeaderCache chainedHeaderCache, IBlockStorageNew blockCache)
        {
            this.logger = logger;
            this.shutdownToken = new CancellationTokenSource();

            this.kernel = kernel;
            this.rules = rules;
            this.blockHeaderCache = blockHeaderCache;
            this.chainedHeaderCache = chainedHeaderCache;
            this.blockCache = blockCache;

            // write genesis block out to storage
            this.blockHeaderCache[this.rules.GenesisBlock.Hash] = this.rules.GenesisBlock.Header;
            this.blockCache[this.rules.GenesisBlock.Hash] = this.rules.GenesisBlock;
            this.chainedHeaderCache[this.rules.GenesisChainedHeader.Hash] = this.rules.GenesisChainedHeader;

            // wire up cache events
            this.blockHeaderCache.OnAddition += OnBlockHeaderAddition;
            this.blockHeaderCache.OnModification += OnBlockHeaderModification;
            this.blockCache.OnAddition += OnBlockAddition;
            this.blockCache.OnModification += OnBlockModification;
            this.chainedHeaderCache.OnAddition += OnChainedHeaderAddition;
            this.chainedHeaderCache.OnModification += OnChainedHeaderModification;

            // create chain state builder
            this.chainStateBuilder =
                this.kernel.Get<ChainStateBuilder>(
                new ConstructorArgument("chain", Chain.CreateForGenesisBlock(this.rules.GenesisChainedHeader).ToBuilder()),
                new ConstructorArgument("parentUtxo", Utxo.CreateForGenesisBlock(this.rules.GenesisBlock.Hash)));

            this.chainStateLock = new ReaderWriterLockSlim();

            // create workers
            this.chainingWorker = kernel.Get<ChainingWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30))));

            this.targetChainWorker = kernel.Get<TargetChainWorker>(
              new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30))));

            this.chainStateWorker = kernel.Get<ChainStateWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.FromSeconds(5))),
                new ConstructorArgument("getTargetChain", (Func<Chain>)(() => this.targetChainWorker.TargetChain)),
                new ConstructorArgument("targetChainWorker", this.targetChainWorker),
                new ConstructorArgument("chainStateBuilder", this.chainStateBuilder));

            this.targetChainWorker.OnTargetBlockChanged +=
                () =>
                {
                    var handler = this.OnTargetBlockChanged;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                };

            this.targetChainWorker.OnTargetChainChanged +=
                () =>
                {
                    this.chainStateWorker.NotifyWork();

                    var handler = this.OnTargetChainChanged;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                };

            this.chainStateWorker.OnChainStateChanged +=
                () =>
                {
                    this.utxoScanWorker.NotifyWork();

                    //TODO once fully synced, this should save off the immutable snapshot immediately
                    //TODO this will allow there to always be an active chain state once synced
                    this.chainStateLock.DoWrite(() =>
                        this.chainState = null);

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
                    var chainStateLocal = this.GetChainState();
                    if (chainStateLocal == null)
                        return;

                    new MethodTimer(this.logger).Time("UTXO Commitment: {0:#,##0}".Format2(chainStateLocal.Utxo.TransactionCount), () =>
                    {
                        using (var utxoStream = new UtxoStream(this.logger, chainStateLocal.Utxo.GetUnspentTransactions()))
                        {
                            var sha256 = new SHA256Managed();
                            var utxoHash = sha256.ComputeHash(utxoStream);
                            this.logger.Info("UXO Commitment Hash: {0}".Format2(utxoHash.ToHexNumberString()));
                        }
                    });

                    //new MethodTimer().Time("Full UTXO Scan: {0:#,##0}".Format2(chainStateLocal.Utxo.OutputCount), () =>
                    //{
                    //    var sha256 = new SHA256Managed();
                    //    foreach (var output in chainStateLocal.Utxo.GetUnspentOutputs())
                    //    {
                    //        if (new UInt256(sha256.ComputeDoubleHash(output.Value.ScriptPublicKey.ToArray())) == UInt256.Zero)
                    //        {
                    //        }
                    //    }
                    //});
                }, initialNotify: true, minIdleTime: TimeSpan.FromSeconds(60), maxIdleTime: TimeSpan.FromSeconds(60), logger: this.logger);
        }

        public ChainedHeader TargetBlock { get { return this.targetChainWorker.TargetBlock; } }

        public int TargetBlockHeight
        {
            get
            {
                var targetBlockLocal = this.targetChainWorker.TargetBlock;
                if (targetBlockLocal != null)
                    return targetBlockLocal.Height;
                else
                    return -1;
            }
        }

        public Chain TargetChain { get { return this.targetChainWorker.TargetChain; } }

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

        public float GetBlockMissRate(TimeSpan perUnitTime)
        {
            return this.chainStateWorker.GetBlockMissRate(perUnitTime);
        }

        public void Start()
        {
            //var blockStorageNew = this.kernel.Get<IBlockStorageNew>();

            //var blockIndex = 0;
            //foreach (var block in this.blockCache.Values)
            //{
            //    this.logger.Info(blockIndex.ToString());

            //    blockStorageNew.AddBlock(block);
            //    blockIndex++;
            //}

            // start loading the existing state from storage
            //TODO LoadExistingState();

            // startup workers
            this.chainingWorker.Start();
            this.targetChainWorker.Start();
            this.chainStateWorker.Start();
            this.gcWorker.Start();
            //this.utxoScanWorker.Start();
        }

        public void Stop()
        {
            // startup workers
            this.chainingWorker.Stop();
            this.targetChainWorker.Stop();
            this.chainStateWorker.Stop();
            this.gcWorker.Stop();
            this.utxoScanWorker.Stop();
        }

        public void Dispose()
        {
            this.Stop();

            // cleanup events
            this.blockHeaderCache.OnAddition -= OnBlockHeaderAddition;
            this.blockHeaderCache.OnModification -= OnBlockHeaderModification;
            this.blockCache.OnAddition -= OnBlockAddition;
            this.blockCache.OnModification -= OnBlockModification;
            this.chainedHeaderCache.OnAddition -= OnChainedHeaderAddition;
            this.chainedHeaderCache.OnModification -= OnChainedHeaderModification;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
                this.chainStateWorker,
                this.prevChainState,
                this.chainState,
                this.targetChainWorker,
                this.chainingWorker,
                this.gcWorker,
                this.utxoScanWorker,
                this.shutdownToken
            }.DisposeList();
        }

        public void ForceWorkAndWait()
        {
            this.chainingWorker.ForceWorkAndWait();
            this.targetChainWorker.ForceWorkAndWait();
            this.chainStateWorker.ForceWorkAndWait();
        }

        public ChainState GetChainState()
        {
            this.chainStateLock.EnterUpgradeableReadLock();
            try
            {
                if (this.chainState == null)
                {
                    this.chainStateLock.EnterWriteLock();
                    try
                    {
                        if (this.chainState == null)
                        {
                            if (this.prevChainState != null)
                                this.prevChainState.Dispose();

                            this.chainState = this.chainStateBuilder.ToImmutable();
                            this.prevChainState = this.chainState;
                        }
                    }
                    finally
                    {
                        this.chainStateLock.ExitWriteLock();
                    }
                }

                return this.chainState;
            }
            finally
            {
                this.chainStateLock.ExitUpgradeableReadLock();
            }
        }

        public IDisposable SubscribeChainStateVisitor(IChainStateVisitor visitor)
        {
            return this.chainStateBuilder.Subscribe(visitor);
        }

        private void OnBlockHeaderAddition(UInt256 blockHash, BlockHeader blockHeader)
        {
            this.chainStateWorker.NotifyWork();
        }

        private void OnBlockHeaderModification(UInt256 blockHash, BlockHeader blockHeader)
        {
            OnBlockHeaderAddition(blockHash, blockHeader);
        }

        private void OnBlockAddition(UInt256 blockHash, Block block)
        {
            this.chainStateWorker.NotifyWork();
        }

        private void OnBlockModification(UInt256 blockHash, Block block)
        {
            OnBlockAddition(blockHash, block);
        }

        private void OnChainedHeaderAddition(UInt256 blockHash, ChainedHeader chainedHeader)
        {
            this.chainStateWorker.NotifyWork();
        }

        private void OnChainedHeaderModification(UInt256 blockHash, ChainedHeader chainedHeader)
        {
            OnChainedHeaderAddition(blockHash, chainedHeader);
        }

        private void LoadExistingState()
        {
            throw new NotImplementedException();

            //var stopwatch = Stopwatch.StartNew();

            ////TODO
            //Tuple<BlockchainKey, BlockchainMetadata> winner = null;

            //foreach (var tuple in this.StorageContext.BlockchainStorage.ListBlockchains())
            //{
            //    if (winner == null)
            //        winner = tuple;

            //    if (tuple.Item2.TotalWork > winner.Item2.TotalWork)
            //    {
            //        winner = tuple;
            //    }
            //}

            //// check if an existing blockchain has been found
            //if (winner != null)
            //{
            //    // read the winning blockchain
            //    var blockchain = this.StorageContext.BlockchainStorage.ReadBlockchain(winner.Item1);
            //    UpdateChainState(new ChainState(blockchain));

            //    // collect after loading
            //    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

            //    // clean up any old blockchains
            //    this.StorageContext.BlockchainStorage.RemoveBlockchains(winner.Item2.TotalWork);

            //    // log statistics
            //    stopwatch.Stop();
            //    Debug.WriteLine(
            //        string.Join("\n",
            //            new string('-', 80),
            //            "Loaded blockchain on startup in {0:#,##0.000} seconds, height: {1:#,##0}, utxo size: {2:#,##0}",
            //            "GC Memory:      {3,10:#,##0.00} MB",
            //            "Process Memory: {4,10:#,##0.00} MB",
            //            new string('-', 80)
            //        )
            //        .Format2
            //        (
            //            stopwatch.ElapsedSecondsFloat(),
            //            blockchain.Height,
            //            blockchain.Utxo.Count,
            //            (float)GC.GetTotalMemory(false) / 1.MILLION(),
            //            (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1.MILLION()
            //        ));
            //}
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