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
        public event EventHandler OnChainStateBuilderChanged;

        private readonly Logger logger;
        private readonly IKernel kernel;
        private readonly IBlockchainRules rules;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly ChainedBlockCache chainedBlockCache;
        private readonly BlockTxHashesCache blockTxHashesCache;
        private readonly TransactionCache transactionCache;
        private readonly BlockCache blockCache;

        private readonly CancellationTokenSource shutdownToken;

        private readonly ChainingWorker chainingWorker;
        private readonly TargetChainWorker targetChainWorker;
        private readonly ChainStateWorker chainStateWorker;
        private readonly WorkerMethod gcWorker;
        private readonly WorkerMethod utxoScanWorker;

        public CoreDaemon(Logger logger, IKernel kernel, IBlockchainRules rules, BlockHeaderCache blockHeaderCache, ChainedBlockCache chainedBlockCache, BlockTxHashesCache blockTxHashesCache, TransactionCache transactionCache, BlockCache blockCache)
        {
            this.logger = logger;
            this.shutdownToken = new CancellationTokenSource();

            this.kernel = kernel;
            this.rules = rules;
            this.blockHeaderCache = blockHeaderCache;
            this.chainedBlockCache = chainedBlockCache;
            this.blockTxHashesCache = blockTxHashesCache;
            this.transactionCache = transactionCache;
            this.blockCache = blockCache;

            // write genesis block out to storage
            this.blockCache[this.rules.GenesisBlock.Hash] = this.rules.GenesisBlock;
            this.chainedBlockCache[this.rules.GenesisChainedBlock.BlockHash] = this.rules.GenesisChainedBlock;

            // wire up cache events
            this.blockHeaderCache.OnAddition += OnBlockHeaderAddition;
            this.blockHeaderCache.OnModification += OnBlockHeaderModification;
            this.blockCache.OnAddition += OnBlockAddition;
            this.blockCache.OnModification += OnBlockModification;
            this.blockTxHashesCache.OnAddition += OnBlockTxHashesAddition;
            this.blockTxHashesCache.OnModification += OnBlockTxHashesModification;
            this.chainedBlockCache.OnAddition += OnChainedBlockAddition;
            this.chainedBlockCache.OnModification += OnChainedBlockModification;

            // create workers
            this.chainingWorker = kernel.Get<ChainingWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30))));

            this.targetChainWorker = kernel.Get<TargetChainWorker>(
              new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30))));

            this.chainStateWorker = kernel.Get<ChainStateWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.FromMinutes(5))),
                new ConstructorArgument("getTargetChain", (Func<Chain>)(() => this.targetChainWorker.TargetChain)),
                new ConstructorArgument("maxBuilderTime", TimeSpan.FromMinutes(30)));

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
                    //this.pruningWorker.NotifyWork();
                    this.utxoScanWorker.NotifyWork();

                    var handler = this.OnChainStateChanged;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                };

            this.chainStateWorker.OnChainStateBuilderChanged +=
                () =>
                {
                    var handler = this.OnChainStateBuilderChanged;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                };

            this.gcWorker = new WorkerMethod("GC Worker",
                () =>
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
                () =>
                {
                    var chainStateLocal = this.ChainState;
                    if (chainStateLocal == null)
                        return;

                    new MethodTimer().Time("Full UTXO Scan: {0:#,##0}".Format2(chainStateLocal.Utxo.OutputCount), () =>
                    {
                        var sha256 = new SHA256Managed();
                        foreach (var output in chainStateLocal.Utxo.GetUnspentOutputs())
                        {
                            if (new UInt256(sha256.ComputeDoubleHash(output.Value.ScriptPublicKey.ToArray())) == UInt256.Zero)
                            {
                            }
                        }
                    });
                }, initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: this.logger);
        }

        public ChainedBlock TargetBlock { get { return this.targetChainWorker.TargetBlock; } }

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

        public ChainState ChainState { get { return this.chainStateWorker.ChainState; } }

        public Chain CurrentBuilderChain
        {
            get
            {
                var chainStateLocal = this.chainStateWorker.ChainState;
                var chainStateBuilderLocal = this.chainStateWorker.ChainStateBuilder;

                if (chainStateBuilderLocal != null)
                    return chainStateBuilderLocal.Chain.ToImmutable();
                else
                    return chainStateLocal.Chain;
            }
        }

        public int CurrentBuilderHeight
        {
            get
            {
                var chainStateLocal = this.chainStateWorker.ChainState;
                var chainStateBuilderLocal = this.chainStateWorker.ChainStateBuilder;

                if (chainStateBuilderLocal != null)
                    return chainStateBuilderLocal.Height;
                else
                    return chainStateLocal.Height;
            }
        }

        public TimeSpan MaxBuilderTime
        {
            get { return this.chainStateWorker.MaxBuilderTime; }
            set { this.chainStateWorker.MaxBuilderTime = value; }
        }

        public TimeSpan AverageBlockProcessingTime()
        {
            return this.chainStateWorker.AverageBlockProcessingTime();
        }

        internal ChainingWorker ChainingWorker
        {
            get { return this.chainingWorker; }
        }

        internal TargetChainWorker TargetChainWorker
        {
            get { return this.targetChainWorker; }
        }

        internal ChainStateWorker ChainStateWorker
        {
            get { return this.chainStateWorker; }
        }

        public void Start()
        {
            try
            {
                // start loading the existing state from storage
                //TODO LoadExistingState();

                // startup workers
                this.chainingWorker.Start();
                this.targetChainWorker.Start();
                this.chainStateWorker.Start();
                this.gcWorker.Start();
                //this.utxoScanWorker.Start();
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
            this.blockHeaderCache.OnAddition -= OnBlockHeaderAddition;
            this.blockHeaderCache.OnModification -= OnBlockHeaderModification;
            this.blockCache.OnAddition -= OnBlockAddition;
            this.blockCache.OnModification -= OnBlockModification;
            this.blockTxHashesCache.OnAddition -= OnBlockTxHashesAddition;
            this.blockTxHashesCache.OnModification -= OnBlockTxHashesModification;
            this.chainedBlockCache.OnAddition -= OnChainedBlockAddition;
            this.chainedBlockCache.OnModification -= OnChainedBlockModification;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
                this.chainingWorker,
                this.targetChainWorker,
                this.chainStateWorker,
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

        private void OnBlockTxHashesAddition(UInt256 blockHash, IImmutableList<UInt256> blockTxHashes)
        {
            this.chainStateWorker.NotifyWork();
        }

        private void OnBlockTxHashesModification(UInt256 blockHash, IImmutableList<UInt256> blockTxHashes)
        {
            OnBlockTxHashesAddition(blockHash, blockTxHashes);
        }

        private void OnChainedBlockAddition(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            this.chainStateWorker.NotifyWork();
        }

        private void OnChainedBlockModification(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            OnChainedBlockAddition(blockHash, chainedBlock);
        }

        private void LoadExistingState()
        {
            throw new NotImplementedException();

            //var stopwatch = new Stopwatch();
            //stopwatch.Start();

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
            //    var stopwatch = new Stopwatch();
            //    stopwatch.Start();

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

            //var stopwatch = new Stopwatch();
            //stopwatch.Start();

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
