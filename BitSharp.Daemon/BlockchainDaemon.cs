using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.Storage.ExtensionMethods;
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
using BitSharp.Data;
using System.IO;

namespace BitSharp.Daemon
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
    public class BlockchainDaemon : IDisposable
    {
        public event EventHandler<ChainState> OnCurrentBlockchainChanged;
        public event EventHandler<int> OnCurrentBuilderHeightChanged;

        private static readonly int MAX_BUILDER_LIFETIME_SECONDS = 60;

        private readonly CacheContext cacheContext;

        private readonly IBlockchainRules rules;
        private readonly BlockchainCalculator calculator;

        private ChainState chainState;
        private readonly ReaderWriterLockSlim chainStateLock;

        private ChainStateBuilder chainStateBuilder;
        private DateTime chainStateBuilderTime;

        private readonly CancellationTokenSource shutdownToken;

        private readonly Worker validationWorker;
        private readonly Worker blockchainWorker;
        private readonly Worker validateCurrentChainWorker;
        private readonly Worker writeBlockchainWorker;
        private readonly TargetChainWorker targetChainWorker;

        public BlockchainDaemon(IBlockchainRules rules, CacheContext cacheContext)
        {
            this.shutdownToken = new CancellationTokenSource();

            this.rules = rules;
            this.cacheContext = cacheContext;
            this.calculator = new BlockchainCalculator(this.rules, this.cacheContext, this.shutdownToken.Token);

            this.chainState = ChainState.CreateForGenesisBlock(this.rules.GenesisChainedBlock, this.StorageContext.ToUtxoBuilder);
            this.chainStateLock = new ReaderWriterLockSlim();

            // write genesis block out to storage
            this.cacheContext.BlockCache[this.rules.GenesisBlock.Hash] = this.rules.GenesisBlock;
            this.cacheContext.ChainedBlockCache[this.rules.GenesisChainedBlock.BlockHash] = this.rules.GenesisChainedBlock;

            // wire up cache events
            this.cacheContext.BlockHeaderCache.OnAddition += OnBlockHeaderAddition;
            this.cacheContext.BlockHeaderCache.OnModification += OnBlockHeaderModification;
            this.cacheContext.BlockCache.OnAddition += OnBlockAddition;
            this.cacheContext.BlockCache.OnModification += OnBlockModification;
            this.cacheContext.ChainedBlockCache.OnAddition += OnChainedBlockAddition;
            this.cacheContext.ChainedBlockCache.OnModification += OnChainedBlockModification;

            // create workers
            this.validationWorker = new Worker("BlockchainDaemon.ValidationWorker", ValidationWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(10), maxIdleTime: TimeSpan.FromMinutes(5));

            this.blockchainWorker = new Worker("BlockchainDaemon.BlockchainWorker", BlockchainWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromMinutes(5));

            this.validateCurrentChainWorker = new Worker("BlockchainDaemon.ValidateCurrentChainWorker", ValidateCurrentChainWorker,
                runOnStart: true, waitTime: TimeSpan.FromMinutes(30), maxIdleTime: TimeSpan.FromMinutes(30));

            this.writeBlockchainWorker = new Worker("BlockchainDaemon.WriteBlockchainWorker", WriteBlockchainWorker,
                runOnStart: true, waitTime: TimeSpan.FromMinutes(5), maxIdleTime: TimeSpan.FromMinutes(30));

            this.targetChainWorker = new TargetChainWorker(rules, cacheContext);
        }

        public IBlockchainRules Rules { get { return this.rules; } }

        public BlockchainCalculator Calculator { get { return this.calculator; } }

        public CacheContext CacheContext { get { return this.cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public ChainState ChainState { get { return this.chainState; } }

        public TargetChainWorker TargetChainWorker { get { return this.targetChainWorker; } }

        public int CurrentBuilderHeight
        {
            get
            {
                var chainStateLocal = this.chainState;
                var chainStateBuilderLocal = this.chainStateBuilder;

                if (chainStateBuilderLocal != null)
                    return chainStateBuilderLocal.ChainedBlocks.Height;
                else
                    return chainStateLocal.CurrentBlock.Height;
            }
        }

        public void Start()
        {
            try
            {
                // start loading the existing state from storage
                //TODO LoadExistingState();

                // startup workers
                //TODO this.validationWorker.Start();
                this.blockchainWorker.Start();
                //TODO this.validateCurrentChainWorker.Start();
                //TODO this.writeBlockchainWorker.Start();
                this.targetChainWorker.Start();
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
            this.CacheContext.BlockHeaderCache.OnAddition -= OnBlockHeaderAddition;
            this.CacheContext.BlockHeaderCache.OnModification -= OnBlockHeaderModification;
            this.CacheContext.BlockCache.OnAddition -= OnBlockAddition;
            this.CacheContext.BlockCache.OnModification -= OnBlockModification;
            this.CacheContext.ChainedBlockCache.OnAddition -= OnChainedBlockAddition;
            this.CacheContext.ChainedBlockCache.OnModification -= OnChainedBlockModification;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
                this.validationWorker,
                this.blockchainWorker,
                this.validateCurrentChainWorker,
                this.writeBlockchainWorker,
                this.targetChainWorker,
                this.shutdownToken
            }.DisposeList();
        }

        public void WaitForFullUpdate()
        {
            WaitForBlockchainUpdate();
        }

        public void WaitForBlockchainUpdate()
        {
            this.blockchainWorker.ForceWorkAndWait();
        }

        private void OnBlockHeaderAddition(UInt256 blockHash, BlockHeader blockHeader)
        {
            this.blockchainWorker.NotifyWork();
        }

        private void OnBlockHeaderModification(UInt256 blockHash, BlockHeader blockHeader)
        {
            OnBlockHeaderAddition(blockHash, blockHeader);
        }

        private void OnBlockAddition(UInt256 blockHash, Block block)
        {
            this.blockchainWorker.NotifyWork();
        }

        private void OnBlockModification(UInt256 blockHash, Block block)
        {
            OnBlockAddition(blockHash, block);
        }

        private void OnChainedBlockAddition(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            this.blockchainWorker.NotifyWork();
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

        private void ValidationWorker()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            stopwatch.Stop();
            Debug.WriteLine("ValidationWorker: {0:#,##0.000}s".Format2(stopwatch.ElapsedSecondsFloat()));
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

        private void BlockchainWorker()
        {
            try
            {
                var chainStateLocal = this.chainState;

                if (this.chainStateBuilder != null
                    && this.chainStateBuilder.ChainedBlocks.LastBlock.BlockHash != chainStateLocal.CurrentBlock.BlockHash
                    && DateTime.UtcNow - this.chainStateBuilderTime > TimeSpan.FromSeconds(MAX_BUILDER_LIFETIME_SECONDS))
                {
                    var newChainedBlocks = this.chainStateBuilder.ChainedBlocks.ToImmutable();
                    var newUtxo = this.chainStateBuilder.Utxo.Close(newChainedBlocks.LastBlock.BlockHash);

                    this.chainStateBuilder.Dispose();
                    this.chainStateBuilder = null;

                    UpdateCurrentBlockchain(new ChainState(newChainedBlocks, newUtxo));
                    chainStateLocal = this.chainState;
                }

                if (this.chainStateBuilder == null)
                {
                    this.chainStateBuilderTime = DateTime.UtcNow;
                    this.chainStateBuilder = chainStateLocal.ToBuilder(this.StorageContext.ToUtxoBuilder);
                }

                // try to advance the blockchain with the new winning block
                using (var cancelToken = new CancellationTokenSource())
                {
                    var startTime = new Stopwatch();
                    startTime.Start();

                    Calculator.CalculateBlockchainFromExisting(this.chainStateBuilder, () => this.targetChainWorker.TargetChainedBlocks, cancelToken.Token,
                        () =>
                        {
                            var handler = this.OnCurrentBuilderHeightChanged;
                            if (handler != null)
                                handler(this, this.chainStateBuilder.ChainedBlocks.Height);

                            if (startTime.Elapsed > TimeSpan.FromSeconds(MAX_BUILDER_LIFETIME_SECONDS))
                            {
                                this.blockchainWorker.NotifyWork();
                                cancelToken.Cancel();
                            }

                            // let the blockchain writer know there is new work
                            this.writeBlockchainWorker.NotifyWork();
                        });
                }
            }
            catch (Exception)
            {
                if (this.chainStateBuilder != null && !this.chainStateBuilder.IsConsistent)
                {
                    this.chainStateBuilder.Dispose();
                    this.chainStateBuilder = null;
                }

                // try again on failure
                this.blockchainWorker.NotifyWork();

                var handler = this.OnCurrentBuilderHeightChanged;
                if (handler != null)
                    handler(this, this.chainState.CurrentChainedBlocks.Height);
            }

            // collect after processing
            //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

            // whenever the chain is successfully advanced, keep looking for more
            //this.blockchainWorker.NotifyWork();

            // kick off a blockchain revalidate after update
            this.validateCurrentChainWorker.NotifyWork();
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

        private void UpdateCurrentBlockchain(ChainState newChainState)
        {
            this.chainStateLock.EnterWriteLock();
            try
            {
                var oldChainState = this.chainState;

                this.chainState = newChainState;

                //TODO stop gap
                if (oldChainState != null)
                    oldChainState.CurrentUtxo.DisposeDelete();
            }
            finally
            {
                this.chainStateLock.ExitWriteLock();
            }

            var handler = this.OnCurrentBlockchainChanged;
            if (handler != null)
                handler(this, newChainState);
        }
    }
}
