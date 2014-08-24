using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class BlockReplayer : IDisposable
    {
        private readonly Logger logger;
        private readonly CoreStorage coreStorage;
        private readonly IBlockchainRules rules;

        private readonly ParallelConsumer<BlockTx> pendingTxLoader;
        private readonly ParallelConsumer<TxWithPrevOutputKeys> txLoader;

        private IChainState chainState;
        private ChainedHeader replayBlock;
        private ConcurrentDictionary<UInt256, Transaction> txCache;
        private ConcurrentBlockingQueue<TxWithPrevOutputKeys> pendingTxes;
        private ConcurrentBlockingQueue<TxWithPrevOutputs> loadedTxes;
        private IDisposable pendingTxLoaderStopper;
        private IDisposable txLoaderStopper;
        private ConcurrentBag<Exception> pendingTxLoaderExceptions;
        private ConcurrentBag<Exception> txLoaderExceptions;

        public BlockReplayer(CoreStorage coreStorage, IBlockchainRules rules, Logger logger)
        {
            this.logger = logger;
            this.coreStorage = coreStorage;
            this.rules = rules;

            // thread count for i/o task (TxLoader)
            var ioThreadCount = 4;

            // thread count for cpu tasks (TxValidator, ScriptValidator)
            var cpuThreadCount = Environment.ProcessorCount * 2;

            this.pendingTxLoader = new ParallelConsumer<BlockTx>("BlockReplayer.PendingTxLoader", ioThreadCount, logger);
            this.txLoader = new ParallelConsumer<TxWithPrevOutputKeys>("BlockReplayer.TxLoader", ioThreadCount, logger);
        }

        public void Dispose()
        {
            new IDisposable[]
            {
                this.pendingTxLoader,
                this.txLoader
            }.DisposeList();
        }

        public IDisposable StartReplay(IChainState chainState, UInt256 blockHash)
        {
            ChainedHeader replayBlock;
            if (!chainState.Chain.BlocksByHash.TryGetValue(blockHash, out replayBlock))
            {
                //TODO when a block is rolled back i'll need to store information to allow the rollback to be replayed, and then look it up here
                throw new Exception("TODO");
            }

            IEnumerable<BlockTx> blockTxes;
            if (!this.coreStorage.TryReadBlockTransactions(replayBlock.Hash, replayBlock.MerkleRoot, /*requireTransaction:*/true, out blockTxes))
            {
                throw new MissingDataException(replayBlock.Hash);
            }

            this.chainState = chainState;
            this.replayBlock = replayBlock;

            this.txCache = new ConcurrentDictionary<UInt256, Transaction>();

            this.pendingTxes = new ConcurrentBlockingQueue<TxWithPrevOutputKeys>();
            this.loadedTxes = new ConcurrentBlockingQueue<TxWithPrevOutputs>();

            this.pendingTxLoaderExceptions = new ConcurrentBag<Exception>();
            this.txLoaderExceptions = new ConcurrentBag<Exception>();

            this.pendingTxLoaderStopper = StartPendingTxLoader(blockTxes);
            this.txLoaderStopper = StartTxLoader();

            return new Stopper(this);
        }

        public IEnumerable<TxWithPrevOutputs> ReplayBlock()
        {
            foreach (var tx in this.loadedTxes.GetConsumingEnumerable())
            {
                yield return tx;
            }
        }

        private void StopReplay()
        {
            this.pendingTxes.CompleteAdding();
            this.loadedTxes.CompleteAdding();

            new IDisposable[]
            {
                this.pendingTxLoaderStopper,
                this.txLoaderStopper,
                this.pendingTxes,
                this.loadedTxes,
            }.DisposeList();

            this.chainState = null;
            this.replayBlock = null;
            this.txCache = null;
            this.pendingTxes = null;
            this.loadedTxes = null;
            this.pendingTxLoaderStopper = null;
            this.txLoaderStopper = null;
            this.pendingTxLoaderExceptions = null;
            this.txLoaderExceptions = null;
        }

        private IDisposable StartPendingTxLoader(IEnumerable<BlockTx> blockTxes)
        {
            return this.pendingTxLoader.Start(blockTxes.LookAhead(10),
                blockTx =>
                {
                    var pendingTx = LoadPendingTx(blockTx);
                    if (pendingTx != null)
                        this.pendingTxes.Add(pendingTx);
                },
                () => this.pendingTxes.CompleteAdding());
        }

        private IDisposable StartTxLoader()
        {
            return this.txLoader.Start(this.pendingTxes.GetConsumingEnumerable(),
                pendingTx =>
                {
                    var loadedTx = LoadPendingTx(pendingTx, txCache);
                    if (loadedTx != null)
                        this.loadedTxes.Add(loadedTx);
                },
                () => this.loadedTxes.CompleteAdding());
        }

        //TODO conflicting names
        private TxWithPrevOutputKeys LoadPendingTx(BlockTx blockTx)
        {
            try
            {
                var tx = blockTx.Transaction;
                var txIndex = blockTx.Index;

                var prevOutputTxKeys = ImmutableArray.CreateBuilder<BlockTxKey>(tx.Inputs.Length);

                if (txIndex > 0)
                {
                    // spend each of the transaction's inputs in the utxo
                    for (var inputIndex = 0; inputIndex < tx.Inputs.Length; inputIndex++)
                    {
                        var input = tx.Inputs[inputIndex];

                        UnspentTx unspentTx;
                        if (!this.chainState.TryGetUnspentTx(input.PreviousTxOutputKey.TxHash, out unspentTx))
                            throw new MissingDataException(this.replayBlock.Hash);

                        var unspentTxBlockHash = this.chainState.Chain.Blocks[unspentTx.BlockIndex].Hash;
                        prevOutputTxKeys.Add(new BlockTxKey(unspentTxBlockHash, unspentTx.TxIndex));
                    }
                }

                var pendingTx = new TxWithPrevOutputKeys(txIndex, tx, this.replayBlock, prevOutputTxKeys.ToImmutable());
                return pendingTx;
            }
            catch (Exception e)
            {
                this.pendingTxLoaderExceptions.Add(e);
                //TODO
                return null;
            }
        }

        private TxWithPrevOutputs LoadPendingTx(TxWithPrevOutputKeys pendingTx, ConcurrentDictionary<UInt256, Transaction> txCache)
        {
            try
            {
                var txIndex = pendingTx.TxIndex;
                var transaction = pendingTx.Transaction;
                var chainedHeader = pendingTx.ChainedHeader;
                var spentTxes = pendingTx.PrevOutputTxKeys;

                var prevTxOutputs = ImmutableArray.CreateBuilder<TxOutput>(transaction.Inputs.Length);

                // load previous transactions for each input, unless this is a coinbase transaction
                if (txIndex > 0)
                {
                    for (var inputIndex = 0; inputIndex < transaction.Inputs.Length; inputIndex++)
                    {
                        var input = transaction.Inputs[inputIndex];

                        Transaction cachedPrevTx;
                        if (txCache.TryGetValue(input.PreviousTxOutputKey.TxHash, out cachedPrevTx))
                        {
                            var prevTxOutput = cachedPrevTx.Outputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()];
                            prevTxOutputs.Add(prevTxOutput);
                        }
                        else
                        {
                            var spentTx = spentTxes[inputIndex];

                            Transaction prevTx;
                            if (this.coreStorage.TryGetTransaction(spentTx.BlockHash, spentTx.TxIndex, out prevTx))
                            {
                                if (input.PreviousTxOutputKey.TxHash != prevTx.Hash)
                                    throw new Exception("TODO");

                                txCache.TryAdd(prevTx.Hash, prevTx);

                                var prevTxOutput = prevTx.Outputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()];
                                prevTxOutputs.Add(prevTxOutput);
                            }
                            else
                            {
                                throw new Exception("TODO");
                            }
                        }
                    }

                    Debug.Assert(prevTxOutputs.Count == transaction.Inputs.Length);
                }

                var txWithPrevOutputs = new TxWithPrevOutputs(txIndex, transaction, chainedHeader, prevTxOutputs.ToImmutableArray());
                return txWithPrevOutputs;
            }
            catch (Exception e)
            {
                this.txLoaderExceptions.Add(e);
                //TODO
                return null;
            }
        }

        private sealed class Stopper : IDisposable
        {
            private readonly BlockReplayer blockReplayer;

            public Stopper(BlockReplayer blockReplayer)
            {
                this.blockReplayer = blockReplayer;
            }

            public void Dispose()
            {
                this.blockReplayer.StopReplay();
            }
        }
    }
}
