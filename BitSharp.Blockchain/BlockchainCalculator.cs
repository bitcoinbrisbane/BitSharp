using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Data;
using System.IO;
using BitSharp.Storage;
using System.Globalization;
using System.Collections;
using NLog;

namespace BitSharp.Blockchain
{
    public class BlockchainCalculator
    {
        private readonly Logger logger;
        private readonly CancellationToken shutdownToken;
        private readonly IBlockchainRules rules;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly BlockTxHashesCache blockTxHashesCache;
        private readonly TransactionCache transactionCache;
        private readonly BlockView blockView;
        private readonly BlockRollbackCache blockRollbackCache;
        private readonly SpentOutputsCache spentOutputsCache;

        public BlockchainCalculator(CancellationToken shutdownToken, Logger logger, IBlockchainRules rules, BlockHeaderCache blockHeaderCache, BlockTxHashesCache blockTxHashesCache, TransactionCache transactionCache, BlockView blockView, BlockRollbackCache blockRollbackCache, SpentOutputsCache spentOutputsCache)
        {
            this.logger = logger;
            this.shutdownToken = shutdownToken;
            this.rules = rules;
            this.blockHeaderCache = blockHeaderCache;
            this.blockTxHashesCache = blockTxHashesCache;
            this.transactionCache = transactionCache;
            this.blockView = blockView;
            this.blockRollbackCache = blockRollbackCache;
            this.spentOutputsCache = spentOutputsCache;
        }

        public void CalculateBlockchainFromExisting(ChainStateBuilder chainStateBuilder, Func<Chain> getTargetChain, CancellationToken cancelToken, Action<TimeSpan> onProgress = null)
        {
            //chainStateBuilder.Stats.totalStopwatch.Start();
            //chainStateBuilder.Stats.currentRateStopwatch.Start();

            // calculate the new blockchain along the target path
            chainStateBuilder.IsConsistent = true;
            foreach (var pathElement in BlockLookAhead(chainStateBuilder.Chain.NavigateTowards(getTargetChain), lookAhead: 1))
            {
                chainStateBuilder.IsConsistent = false;
                var startTime = DateTime.UtcNow;

                // cooperative loop
                if (this.shutdownToken.IsCancellationRequested)
                    break;
                if (cancelToken.IsCancellationRequested)
                    break;

                // get block and metadata for next link in blockchain
                var direction = pathElement.Item1;
                var chainedBlock = pathElement.Item2;
                var block = pathElement.Item3;

                if (direction < 0)
                {
                    RollbackUtxo(chainStateBuilder, block);

                    chainStateBuilder.Chain.RemoveBlock(chainedBlock);
                }
                else if (direction > 0)
                {
                    // add the block to the current chain
                    chainStateBuilder.Chain.AddBlock(chainedBlock);

                    // validate the block
                    chainStateBuilder.Stats.validateStopwatch.Start();
                    new MethodTimer(false).Time("ValidateBlock", () =>
                        this.rules.ValidateBlock(block, chainStateBuilder));
                    chainStateBuilder.Stats.validateStopwatch.Stop();

                    // calculate the new block utxo, double spends will be checked for
                    long txCount = 0, inputCount = 0;
                    new MethodTimer(false).Time("CalculateUtxo", () =>
                        CalculateUtxo(chainedBlock, block, chainStateBuilder.Utxo, out txCount, out inputCount));

                    // collect rollback informatino and store it
                    chainStateBuilder.Utxo.SaveRollbackInformation(block.Hash, this.blockRollbackCache, this.spentOutputsCache);

                    //TODO remove the block and its transactions immediately after processing
                    //TODO this is for startup mode where blocks are not saved but kept in memory just long enough to update the UTXO
                    //TODO when fully caught up, rollback blocks can be reacquired backwards from that point, to the pruning limit
                    //TODO reacquiring is not required, but if a rollback does occur then the information will need to be downloaded then
                    if (true)
                    {
                        foreach (var tx in block.Transactions)
                            this.transactionCache.TryRemove(tx.Hash);
                        this.blockTxHashesCache.TryRemove(block.Hash);
                    }

                    // flush utxo progress
                    //chainStateBuilder.Utxo.Flush();

                    // create the next link in the new blockchain
                    if (onProgress != null)
                        onProgress(DateTime.UtcNow - startTime);

                    // blockchain processing statistics
                    chainStateBuilder.Stats.blockCount++;
                    chainStateBuilder.Stats.txCount += txCount;
                    chainStateBuilder.Stats.inputCount += inputCount;

                    var txInterval = TimeSpan.FromSeconds(15);
                    if (DateTime.UtcNow - chainStateBuilder.Stats.lastLogTime >= txInterval)
                    {
                        LogBlockchainProgress(chainStateBuilder);
                        chainStateBuilder.Stats.lastLogTime = DateTime.UtcNow;
                    }
                }
                else
                    throw new InvalidOperationException();

                chainStateBuilder.IsConsistent = true;
            }
        }

        public void LogBlockchainProgress(ChainStateBuilder chainStateBuilder)
        {
            var elapsedSeconds = chainStateBuilder.Stats.durationStopwatch.ElapsedSecondsFloat();
            var blockRate = (float)chainStateBuilder.Stats.blockCount / elapsedSeconds;
            var txRate = (float)chainStateBuilder.Stats.txCount / elapsedSeconds;
            var inputRate = (float)chainStateBuilder.Stats.inputCount / elapsedSeconds;

            this.logger.Info(
                string.Join("\n",
                    new string('-', 200),
                    "Height: {0,10} | Duration: {1} /*| Validation: {2} */| Blocks/s: {3,7} | Tx/s: {4,7} | Inputs/s: {5,7} | Processed Tx: {6,7} | Processed Inputs: {7,7} | Utxo Size: {8,7}",
                    new string('-', 200)
                )
                .Format2
                (
                /*0*/ chainStateBuilder.Height.ToString("#,##0"),
                /*1*/ chainStateBuilder.Stats.durationStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*2*/ chainStateBuilder.Stats.validateStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*3*/ blockRate.ToString("#,##0"),
                /*4*/ txRate.ToString("#,##0"),
                /*5*/ inputRate.ToString("#,##0"),
                /*6*/ chainStateBuilder.Stats.txCount.ToString("#,##0"),
                /*7*/ chainStateBuilder.Stats.inputCount.ToString("#,##0"),
                /*8*/ chainStateBuilder.Utxo.OutputCount.ToString("#,##0")
                ));
        }

        private void CalculateUtxo(ChainedBlock chainedBlock, Block block, UtxoBuilder utxoBuilder, out long txCount, out long inputCount)
        {
            txCount = 1;
            inputCount = 0;

            // don't include genesis block coinbase in utxo
            if (chainedBlock.Height > 0)
            {
                //TODO apply real coinbase rule
                // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                var coinbaseTx = block.Transactions[0];

                utxoBuilder.Mint(coinbaseTx, chainedBlock);
            }

            // check for double spends
            for (var txIndex = 1; txIndex < block.Transactions.Count; txIndex++)
            {
                var tx = block.Transactions[txIndex];
                txCount++;

                for (var inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                {
                    var input = tx.Inputs[inputIndex];
                    inputCount++;

                    utxoBuilder.Spend(input, chainedBlock);
                }

                utxoBuilder.Mint(tx, chainedBlock);
            }
        }

        //TODO with the rollback information that's now being stored, rollback could be down without needing the block
        private void RollbackUtxo(ChainStateBuilder chainStateBuilder, Block block)
        {
            var blockHeight = chainStateBuilder.Height;
            var utxoBuilder = chainStateBuilder.Utxo;

            //TODO currently a MissingDataException will get thrown if the rollback information is missing
            //TODO rollback is still possible if any resurrecting transactions can be found
            //TODO the network does not allow arbitrary transaction lookup, but if the transactions can be retrieved then this code should allow it
            var blockRollbackInformation = this.blockRollbackCache[block.Hash];
            var blockRollbackDictionary = new Dictionary<UInt256, UInt256>();
            blockRollbackDictionary.AddRange(blockRollbackInformation);

            for (var txIndex = block.Transactions.Count - 1; txIndex >= 1; txIndex--)
            {
                var tx = block.Transactions[txIndex];

                // remove outputs
                utxoBuilder.Unmint(tx, chainStateBuilder.LastBlock);

                // remove inputs in reverse order
                for (var inputIndex = tx.Inputs.Count - 1; inputIndex >= 0; inputIndex--)
                {
                    var input = tx.Inputs[inputIndex];
                    utxoBuilder.Unspend(input, chainStateBuilder.LastBlock, blockRollbackDictionary);
                }
            }

            // remove coinbase outputs
            var coinbaseTx = block.Transactions[0];
            utxoBuilder.Unmint(coinbaseTx, chainStateBuilder.LastBlock);
        }

        public void RevalidateBlockchain(Chain blockchain, Block genesisBlock)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                //TODO delete corrupted data? could get stuck in a fail-loop on the winning chain otherwise

                // verify blockchain has blocks
                if (blockchain.Blocks.Count == 0)
                    throw new ValidationException(0);

                // verify genesis block hash
                if (blockchain.Blocks[0].BlockHash != genesisBlock.Hash)
                    throw new ValidationException(blockchain.Blocks[0].BlockHash);

                // get genesis block header
                var chainGenesisBlockHeader = this.blockHeaderCache[blockchain.Blocks[0].BlockHash];

                // verify genesis block header
                if (
                    genesisBlock.Header.Version != chainGenesisBlockHeader.Version
                    || genesisBlock.Header.PreviousBlock != chainGenesisBlockHeader.PreviousBlock
                    || genesisBlock.Header.MerkleRoot != chainGenesisBlockHeader.MerkleRoot
                    || genesisBlock.Header.Time != chainGenesisBlockHeader.Time
                    || genesisBlock.Header.Bits != chainGenesisBlockHeader.Bits
                    || genesisBlock.Header.Nonce != chainGenesisBlockHeader.Nonce
                    || genesisBlock.Hash != chainGenesisBlockHeader.Hash
                    || genesisBlock.Hash != CalculateHash(chainGenesisBlockHeader))
                {
                    throw new ValidationException(chainGenesisBlockHeader.Hash);
                }

                // setup expected previous block hash value to verify each chain actually does link
                var expectedPreviousBlockHash = genesisBlock.Header.PreviousBlock;
                for (var height = 0; height < blockchain.Blocks.Count; height++)
                {
                    // cooperative loop
                    this.shutdownToken.ThrowIfCancellationRequested();

                    // get the current link in the chain
                    var chainedBlock = blockchain.Blocks[height];

                    // verify height
                    if (chainedBlock.Height != height)
                        throw new ValidationException(chainedBlock.BlockHash);

                    // verify blockchain linking
                    if (chainedBlock.PreviousBlockHash != expectedPreviousBlockHash)
                        throw new ValidationException(chainedBlock.BlockHash);

                    // verify block exists
                    var blockHeader = this.blockHeaderCache[chainedBlock.BlockHash];

                    // verify block metadata matches header values
                    if (blockHeader.PreviousBlock != chainedBlock.PreviousBlockHash)
                        throw new ValidationException(chainedBlock.BlockHash);

                    // verify block header hash
                    if (CalculateHash(blockHeader) != chainedBlock.BlockHash)
                        throw new ValidationException(chainedBlock.BlockHash);

                    // next block metadata should have the current metadata's hash as its previous hash value
                    expectedPreviousBlockHash = chainedBlock.BlockHash;
                }

                // all validation passed
            }
            finally
            {
                stopwatch.Stop();
                this.logger.Info("Blockchain revalidation: {0:#,##0.000000}s".Format2(stopwatch.ElapsedSecondsFloat()));
            }
        }

        public IEnumerable<Tuple<int, ChainedBlock, Block>> BlockLookAhead(IEnumerable<Tuple<int, ChainedBlock>> chain, int lookAhead)
        {
            return chain
                .Select(
                    chainedBlockTuple =>
                    {
                        try
                        {
                            var chainedBlockDirection = chainedBlockTuple.Item1;
                            var chainedBlock = chainedBlockTuple.Item2;

                            var block = new MethodTimer(false).Time("GetBlock", () =>
                                this.blockView[chainedBlock.BlockHash]);

                            return Tuple.Create(chainedBlockDirection, chainedBlock, block);
                        }
                        catch (MissingDataException e)
                        {
                            this.logger.Info("Stalled, MissingDataException: {0}".Format2(e.Key));
                            throw;
                        }
                    })
                .LookAhead(lookAhead, this.shutdownToken);
        }

        private UInt256 CalculateHash(BlockHeader blockHeader)
        {
            return new UInt256(Crypto.DoubleSHA256(DataCalculator.EncodeBlockHeader(blockHeader)));
        }
    }
}
