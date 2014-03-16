using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain;
using BitSharp.Blockchain.ExtensionMethods;
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
        public event EventHandler<ChainedBlock> OnWinningBlockChanged;
        public event EventHandler<ChainState> OnCurrentBlockchainChanged;

        private static readonly int MAX_BUILDER_LIFETIME_SECONDS = 60;

        private readonly CacheContext _cacheContext;

        private readonly IBlockchainRules _rules;
        private readonly BlockchainCalculator _calculator;

        private ChainedBlocks targetChainedBlocks;

        private ChainState chainState;
        private readonly ReaderWriterLockSlim chainStateLock;

        private ChainStateBuilder chainStateBuilder;
        private DateTime chainStateBuilderTime;

        private readonly Dictionary<UInt256, Dictionary<UInt256, BlockHeader>> unchainedBlocksByPrevious;
        private readonly ReaderWriterLockSlim unchainedBlocksLock;

        private readonly ConcurrentSetBuilder<UInt256> missingBlocks;
        private readonly ConcurrentSet<UInt256> missingChainedBlocks;
        private readonly ConcurrentSetBuilder<UInt256> missingTransactions;

        private readonly CancellationTokenSource shutdownToken;

        private readonly Worker winnerWorker;
        private readonly Worker validationWorker;
        private readonly Worker blockchainWorker;
        private readonly Worker validateCurrentChainWorker;
        private readonly Worker writeBlockchainWorker;

        public BlockchainDaemon(IBlockchainRules rules, CacheContext cacheContext)
        {
            this.shutdownToken = new CancellationTokenSource();

            this._rules = rules;
            this._cacheContext = cacheContext;
            this._calculator = new BlockchainCalculator(this._rules, this._cacheContext, this.shutdownToken.Token);

            this.targetChainedBlocks = ChainedBlocks.CreateForGenesisBlock(this._rules.GenesisChainedBlock);

            this.chainState = ChainState.CreateForGenesisBlock(this._rules.GenesisChainedBlock, this.StorageContext.ToUtxoBuilder);
            this.chainStateLock = new ReaderWriterLockSlim();

            this.unchainedBlocksByPrevious = new Dictionary<UInt256, Dictionary<UInt256, BlockHeader>>();
            this.unchainedBlocksLock = new ReaderWriterLockSlim();

            this.missingBlocks = new ConcurrentSetBuilder<UInt256>();
            this.missingChainedBlocks = new ConcurrentSet<UInt256>();
            this.missingTransactions = new ConcurrentSetBuilder<UInt256>();

            // write genesis block out to storage
            this._cacheContext.BlockCache[this._rules.GenesisBlock.Hash] = this._rules.GenesisBlock;
            this._cacheContext.ChainedBlockCache[this._rules.GenesisChainedBlock.BlockHash] = this._rules.GenesisChainedBlock;

            // wire up cache events
            this._cacheContext.BlockHeaderCache.OnAddition += OnBlockHeaderAddition;
            this._cacheContext.BlockHeaderCache.OnModification += OnBlockHeaderModification;
            this._cacheContext.BlockCache.OnAddition += OnBlockAddition;
            this._cacheContext.BlockCache.OnModification += OnBlockModification;
            this._cacheContext.ChainedBlockCache.OnAddition += OnChainedBlockAddition;
            this._cacheContext.ChainedBlockCache.OnModification += OnChainedBlockModification;
            this._cacheContext.ChainedBlockCache.OnMaxTotalWorkBlocksChanged += OnMaxTotalWorkBlocksChanged;

            // create workers
            this.winnerWorker = new Worker("BlockchainDaemon.WinnerWorker", WinnerWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30));

            this.validationWorker = new Worker("BlockchainDaemon.ValidationWorker", ValidationWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(10), maxIdleTime: TimeSpan.FromMinutes(5));

            this.blockchainWorker = new Worker("BlockchainDaemon.BlockchainWorker", BlockchainWorker,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(1), maxIdleTime: TimeSpan.FromMinutes(5));

            this.validateCurrentChainWorker = new Worker("BlockchainDaemon.ValidateCurrentChainWorker", ValidateCurrentChainWorker,
                runOnStart: true, waitTime: TimeSpan.FromMinutes(30), maxIdleTime: TimeSpan.FromMinutes(30));

            this.writeBlockchainWorker = new Worker("BlockchainDaemon.WriteBlockchainWorker", WriteBlockchainWorker,
                runOnStart: true, waitTime: TimeSpan.FromMinutes(5), maxIdleTime: TimeSpan.FromMinutes(30));

            new Thread(
                () => ChainMissingBlocks())
                .Start();
        }

        public IBlockchainRules Rules { get { return this._rules; } }

        public BlockchainCalculator Calculator { get { return this._calculator; } }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public ChainState ChainState { get { return this.chainState; } }

        public ChainedBlocks TargetChainedBlocks { get { return this.targetChainedBlocks; } }

        public ChainedBlock WinningBlock { get { return this.targetChainedBlocks.LastBlock; } }

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

        public ImmutableHashSet<UInt256> MissingBlocks
        {
            get
            {
                return this.missingBlocks.ToImmutable();
            }
        }

        public ImmutableHashSet<UInt256> MissingTransactions
        {
            get
            {
                return this.missingTransactions.ToImmutable();
            }
        }

        public void Start()
        {
            try
            {
                // start loading the existing state from storage
                //TODO LoadExistingState();

                // startup workers
                this.winnerWorker.Start();
                //TODO this.validationWorker.Start();
                this.blockchainWorker.Start();
                //TODO this.validateCurrentChainWorker.Start();
                //TODO this.writeBlockchainWorker.Start();
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
                this.winnerWorker,
                this.validationWorker,
                this.blockchainWorker,
                this.validateCurrentChainWorker,
                this.writeBlockchainWorker,
                this.shutdownToken
            }.DisposeList();
        }

        public void WaitForFullUpdate()
        {
            WaitForWinnerUpdate();
            WaitForBlockchainUpdate();
        }

        public void WaitForWinnerUpdate()
        {
            this.winnerWorker.ForceWorkAndWait();
        }

        public void WaitForBlockchainUpdate()
        {
            this.blockchainWorker.ForceWorkAndWait();
        }

        private void OnBlockHeaderAddition(UInt256 blockHash, BlockHeader blockHeader)
        {
            ChainBlockHeader(blockHash, blockHeader);
        }

        private void OnBlockHeaderModification(UInt256 blockHash, BlockHeader blockHeader)
        {
            OnBlockHeaderAddition(blockHash, blockHeader);
        }

        private void OnBlockAddition(UInt256 blockHash, Block block)
        {
            this.missingBlocks.Remove(blockHash);

            ChainBlockHeader(blockHash, null);

            this.blockchainWorker.NotifyWork();
        }

        private void OnBlockModification(UInt256 blockHash, Block block)
        {
            OnBlockAddition(blockHash, block);
        }

        private void OnChainedBlockAddition(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            this.missingChainedBlocks.TryRemove(blockHash);

            this.blockchainWorker.NotifyWork();
            this.winnerWorker.NotifyWork();
        }

        private void OnChainedBlockModification(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            OnChainedBlockAddition(blockHash, chainedBlock);
        }

        private void OnMaxTotalWorkBlocksChanged()
        {
            this.winnerWorker.NotifyWork();
        }

        private void ChainMissingBlocks()
        {
            foreach (var unchainedBlockHash in
                this.CacheContext.BlockHeaderCache.Keys
                .Except(this.CacheContext.ChainedBlockCache.Keys))
            {
                BlockHeader unchainedBlockHeader;
                if (this.CacheContext.BlockHeaderCache.TryGetValue(unchainedBlockHash, out unchainedBlockHeader))
                {
                    ChainBlockHeader(unchainedBlockHash, unchainedBlockHeader);
                }
            }
        }

        private void ChainBlockHeader(UInt256 blockHash, BlockHeader blockHeader)
        {
            if (blockHeader == null
                && !this.CacheContext.BlockHeaderCache.TryGetValue(blockHash, out blockHeader))
                return;

            this.unchainedBlocksLock.DoWrite(() =>
            {
                var workBlockList = new List<BlockHeader>();

                workBlockList.Add(blockHeader);
                while (workBlockList.Count > 0)
                {
                    var workBlock = workBlockList[0];
                    workBlockList.RemoveAt(0);

                    if (!this.CacheContext.ChainedBlockCache.ContainsKey(workBlock.Hash))
                    {
                        ChainedBlock prevChainedBlock;
                        if (this.CacheContext.ChainedBlockCache.TryGetValue(workBlock.PreviousBlock, out prevChainedBlock))
                        {
                            var newChainedBlock = new ChainedBlock
                            (
                                blockHash: workBlock.Hash,
                                previousBlockHash: workBlock.PreviousBlock,
                                height: prevChainedBlock.Height + 1,
                                totalWork: prevChainedBlock.TotalWork + workBlock.CalculateWork()
                            );

                            this.CacheContext.ChainedBlockCache[workBlock.Hash] = newChainedBlock;

                            if (this.unchainedBlocksByPrevious.ContainsKey(workBlock.Hash))
                            {
                                workBlockList.AddRange(this.unchainedBlocksByPrevious[workBlock.Hash].Values);
                            }

                            if (this.unchainedBlocksByPrevious.ContainsKey(workBlock.PreviousBlock))
                            {
                                this.unchainedBlocksByPrevious[workBlock.PreviousBlock].Remove(workBlock.Hash);
                                if (this.unchainedBlocksByPrevious[workBlock.PreviousBlock].Count == 0)
                                    this.unchainedBlocksByPrevious.Remove(workBlock.PreviousBlock);
                            }
                        }
                        else
                        {
                            if (!this.unchainedBlocksByPrevious.ContainsKey(workBlock.PreviousBlock))
                                this.unchainedBlocksByPrevious[workBlock.PreviousBlock] = new Dictionary<UInt256, BlockHeader>();
                            this.unchainedBlocksByPrevious[workBlock.PreviousBlock][workBlock.Hash] = workBlock;
                        }
                    }
                    else
                    {
                        if (this.unchainedBlocksByPrevious.ContainsKey(workBlock.Hash))
                        {
                            workBlockList.AddRange(this.unchainedBlocksByPrevious[workBlock.Hash].Values);
                        }
                    }
                }
            });
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

        private void WinnerWorker()
        {
            var maxTotalWorkBlocksLocal = this.CacheContext.ChainedBlockCache.MaxTotalWorkBlocks;
            var targetBlockLocal = this.WinningBlock;

            // check if winning block has changed
            if (!maxTotalWorkBlocksLocal.Contains(targetBlockLocal.BlockHash))
            {
                //TODO pick properly when more than one
                var maxTotalWorkBlockHash = maxTotalWorkBlocksLocal.FirstOrDefault();

                // get winning chained block
                if (maxTotalWorkBlockHash != null)
                {
                    UpdateWinningBlockchain(maxTotalWorkBlockHash);
                }
            }
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

        private Stopwatch validateStopwatch = new Stopwatch();
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

                //TODO cleanup this design
                List<MissingDataException> missingData;

                if (this.chainStateBuilder == null)
                {
                    this.chainStateBuilderTime = DateTime.UtcNow;
                    this.chainStateBuilder = chainStateLocal.ToBuilder(this.StorageContext.ToUtxoBuilder);
                }

                try
                {
                    // try to advance the blockchain with the new winning block
                    using (var cancelToken = new CancellationTokenSource())
                    {
                        var startTime = new Stopwatch();
                        startTime.Start();

                        Calculator.CalculateBlockchainFromExisting(this.chainStateBuilder, () => this.targetChainedBlocks, out missingData, cancelToken.Token,
                            () =>
                            {
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
                    this.chainStateBuilder.Dispose();
                    this.chainStateBuilder = null;
                    //this.currentBlockchainPathBuilder = null;
                    throw;
                }

                // collect after processing
                //GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

                // handle any missing data that prevented further processing
                foreach (var e in missingData)
                {
                    HandleMissingData(e);
                }

                // whenever the chain is successfully advanced, keep looking for more
                //this.blockchainWorker.NotifyWork();

                // kick off a blockchain revalidate after update
                this.validateCurrentChainWorker.NotifyWork();
            }
            catch (ValidationException)
            {
                //TODO
                // an invalid blockchain with winning work will just keep trying over and over again until this is implemented
            }
            catch (MissingDataException e)
            {
                HandleMissingData(e);
            }
            catch (AggregateException e)
            {
                foreach (var missingDataException in e.InnerExceptions.OfType<MissingDataException>())
                {
                    HandleMissingData(missingDataException);
                }

                //TODO
                //var validationException = e.InnerExceptions.FirstOrDefault(x => x is ValidationException);
                //if (validationException != null)
                //    throw validationException;

                //TODO
                //throw;
            }
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

        public bool TryGetBlock(UInt256 blockHash, out Block block)
        {
            if (this.CacheContext.BlockCache.TryGetValue(blockHash, out block))
            {
                this.missingBlocks.Remove(blockHash);
                return true;
            }
            else
            {
                this.missingBlocks.Add(blockHash);
                block = default(Block);
                return false;
            }
        }

        public bool TryGetBlockHeader(UInt256 blockHash, out BlockHeader blockHeader)
        {
            Block block;
            if (this.CacheContext.BlockHeaderCache.TryGetValue(blockHash, out blockHeader))
            {
                this.missingBlocks.Remove(blockHash);
                return true;
            }
            else if (this.CacheContext.BlockCache.TryGetValue(blockHash, out block))
            {
                blockHeader = block.Header;
                this.missingBlocks.Remove(blockHash);
                return true;
            }
            else
            {
                this.missingBlocks.Add(blockHash);
                blockHeader = default(BlockHeader);
                return false;
            }
        }

        public bool TryGetChainedBlock(UInt256 blockHash, out ChainedBlock chainedBlock)
        {
            if (this.CacheContext.ChainedBlockCache.TryGetValue(blockHash, out chainedBlock))
            {
                this.missingChainedBlocks.TryRemove(blockHash);
                return true;
            }
            else
            {
                this.missingChainedBlocks.TryAdd(blockHash);
                if (!this.CacheContext.BlockCache.ContainsKey(blockHash))
                    this.missingBlocks.Add(blockHash);

                chainedBlock = default(ChainedBlock);
                return false;
            }
        }

        public bool TryGetTransaction(UInt256 txHash, out Transaction transaction)
        {
            if (this.CacheContext.TransactionCache.TryGetValue(txHash, out transaction))
            {
                this.missingTransactions.Remove(txHash);
                return true;
            }
            else
            {
                this.missingTransactions.Add(txHash);
                transaction = default(Transaction);
                return false;
            }
        }

        public void AddMissingBlock(UInt256 blockHash)
        {
            if (!this.CacheContext.BlockCache.ContainsKey(blockHash))
                this.missingBlocks.Add(blockHash);
        }

        private void HandleMissingData(MissingDataException e)
        {
            switch (e.DataType)
            {
                case DataType.Block:
                case DataType.BlockHeader:
                    this.missingBlocks.Add(e.DataKey);
                    break;

                case DataType.ChainedBlock:
                    this.missingChainedBlocks.TryAdd(e.DataKey);
                    break;

                case DataType.Transaction:
                    this.missingTransactions.Add(e.DataKey);
                    break;
            }
        }

        //private void UpdateWinningBlock(ChainedBlock winningBlock)
        //{
        //    this.winningBlockchainLock.EnterWriteLock();
        //    try
        //    {
        //        this._winningBlockchain = default(ImmutableList<ChainedBlock>);
        //    }
        //    finally
        //    {
        //        this.winningBlockchainLock.ExitWriteLock();
        //    }

        //    this._winningBlock = winningBlock;

        //    // notify the blockchain worker after updating winning block
        //    this.blockchainWorker.NotifyWork();

        //    var handler = this.OnWinningBlockChanged;
        //    if (handler != null)
        //        handler(this, winningBlock);
        //}

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

        private void UpdateWinningBlockchain(UInt256 targetBlockHash)
        {
            try
            {
                ChainedBlock targetBlock;
                if (!this.CacheContext.ChainedBlockCache.TryGetValue(targetBlockHash, out targetBlock))
                    return;

                var newTargetChainedBlocks = this.targetChainedBlocks.ToBuilder();

                var deltaBlockPath = new BlockchainWalker().GetBlockchainPath(newTargetChainedBlocks.LastBlock, targetBlock, blockHash => this.CacheContext.GetChainedBlock(blockHash));

                foreach (var rewindBlock in deltaBlockPath.RewindBlocks)
                    newTargetChainedBlocks.RemoveBlock(rewindBlock);
                foreach (var rewindBlock in deltaBlockPath.AdvanceBlocks)
                    newTargetChainedBlocks.AddBlock(rewindBlock);

                //Debug.WriteLine("Winning chained block {0} at height {1}, total work: {2}".Format2(targetBlock.BlockHash.ToHexNumberString(), targetBlock.Height, targetBlock.TotalWork.ToString("X")));
                this.targetChainedBlocks = newTargetChainedBlocks.ToImmutable();

                this.blockchainWorker.NotifyWork();

                var handler = this.OnWinningBlockChanged;
                if (handler != null)
                    handler(this, targetBlock);
            }
            catch (MissingDataException e)
            {
                HandleMissingData(e);
            }
            catch (AggregateException e)
            {
                foreach (var missingDataException in e.InnerExceptions.OfType<MissingDataException>())
                {
                    HandleMissingData(missingDataException);
                }

                if (e.InnerExceptions.Any(x => !(x is MissingDataException)))
                    throw;
            }
        }
    }
}
