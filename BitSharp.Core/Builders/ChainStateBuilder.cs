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
using System.IO;
using System.Globalization;
using System.Collections;
using NLog;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Workers;
using System.Security.Cryptography;
using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using BitSharp.Domain;
using Ninject;
using Ninject.Parameters;

namespace BitSharp.Core.Builders
{
    public class ChainStateBuilder : IDisposable
    {
        private static readonly int DUPE_COINBASE_1_HEIGHT = 91722;
        private static readonly UInt256 DUPE_COINBASE_1_HASH = UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber);
        private static readonly int DUPE_COINBASE_2_HEIGHT = 91812;
        private static readonly UInt256 DUPE_COINBASE_2_HASH = UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber);

        private readonly Logger logger;
        private readonly SHA256Managed sha256;
        private readonly IBlockchainRules rules;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly IBlockStorageNew blockCache;
        //private readonly SpentTransactionsCache spentTransactionsCache;
        //private readonly SpentOutputsCache spentOutputsCache;

        //private readonly ChainStateMonitor chainStateMonitor;
        //private readonly ScriptValidator scriptValidator;

        private bool inTransaction;
        private ChainBuilder chain;
        private Chain savedChain;
        private readonly IChainStateBuilderStorage chainStateBuilderStorage;

        //TODO when written more directly against Esent, these can be streamed out so an entire list doesn't need to be held in memory
        //private readonly ImmutableList<KeyValuePair<UInt256, SpentTx>>.Builder spentTransactions;
        //private readonly ImmutableList<KeyValuePair<TxOutputKey, TxOutput>>.Builder spentOutputs;

        private readonly ReaderWriterLockSlim commitLock;

        private readonly BuilderStats stats;

        public ChainStateBuilder(ChainBuilder chain, Utxo parentUtxo, Logger logger, IKernel kernel, IBlockchainRules rules, BlockHeaderCache blockHeaderCache, IBlockStorageNew blockCache, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
        {
            this.logger = logger;
            this.sha256 = new SHA256Managed();
            this.rules = rules;
            this.blockHeaderCache = blockHeaderCache;
            this.blockCache = blockCache;
            //this.spentTransactionsCache = spentTransactionsCache;
            //this.spentOutputsCache = spentOutputsCache;

            //this.chainStateMonitor = new ChainStateMonitor(this.logger);
            //this.scriptValidator = new ScriptValidator(this.logger, this.rules);
            //this.chainStateMonitor.Subscribe(this.scriptValidator);

            this.chain = chain;
            this.chainStateBuilderStorage = kernel.Get<IChainStateBuilderStorage>(new ConstructorArgument("parentUtxo", parentUtxo.Storage));

            //this.spentTransactions = ImmutableList.CreateBuilder<KeyValuePair<UInt256, SpentTx>>();
            //this.spentOutputs = ImmutableList.CreateBuilder<KeyValuePair<TxOutputKey, TxOutput>>();

            this.commitLock = new ReaderWriterLockSlim();

            this.stats = new BuilderStats();
        }

        ~ChainStateBuilder()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            //if (this.chainStateMonitor != null)
            //    this.chainStateMonitor.Dispose();
            //if (this.scriptValidator != null)
            //    this.scriptValidator.Dispose();

            this.stats.Dispose();
            this.chainStateBuilderStorage.Dispose();
        }

        public ChainBuilder Chain { get { return this.chain; } }

        public ChainedHeader LastBlock { get { return this.chain.LastBlock; } }

        public UInt256 LastBlockHash
        {
            get
            {
                var lastBlockLocal = this.LastBlock;
                if (lastBlockLocal != null)
                    return this.LastBlock.Hash;
                else
                    return UInt256.Zero;
            }
        }

        public int Height
        {
            get
            {
                var lastBlockLocal = this.LastBlock;
                if (lastBlockLocal != null)
                    return this.LastBlock.Height;
                else
                    return -1;
            }
        }

        public BuilderStats Stats { get { return this.stats; } }

        public IDisposable Subscribe(IChainStateVisitor visitor)
        {
            return null;

            //if (this.chainStateMonitor == null)
            //    throw new InvalidOperationException();

            //return this.chainStateMonitor.Subscribe(visitor);
        }

        public void AddBlock(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes)
        {
            //using (this.chainStateMonitor.Start())
            //using (this.scriptValidator.Start())
            {
                this.BeginTransaction();
                try
                {
                    // MONITOR: BeginBlock
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.BeginBlock(chainedBlock.ChainedHeader);

                    // add the block to the chain
                    this.Chain.AddBlock(chainedHeader);

                    // store block hash
                    this.chainStateBuilderStorage.BlockHeight = chainedHeader.Height;
                    this.chainStateBuilderStorage.BlockHash = chainedHeader.Hash;

                    // validate the block
                    //this.Stats.validateStopwatch.Start();
                    //new MethodTimer(false).Time("ValidateBlock", () =>
                    //    this.rules.ValidateBlock(chainedBlock, this));
                    //this.Stats.validateStopwatch.Stop();

                    // calculate the new block utxo, double spends will be checked for
                    long txCount = 0, inputCount = 0;
                    new MethodTimer(false).Time("CalculateUtxo", () =>
                        this.CalculateUtxo(chainedHeader, blockTxes, out txCount, out inputCount));

                    // collect rollback informatino and store it
                    //this.SaveRollbackInformation(chainedBlock.Hash, this.spentTransactionsCache, this.spentOutputsCache);

                    // wait for monitor events to finish
                    //this.chainStateMonitor.CompleteAdding();
                    //this.chainStateMonitor.WaitToComplete();

                    // check script validation results
                    //this.scriptValidator.CompleteAdding();
                    //this.scriptValidator.WaitToComplete();
                    //if (this.scriptValidator.ValidationExceptions.Count > 0)
                    //{
                    //    if (!MainnetRules.IgnoreScriptErrors)
                    //        throw new AggregateException(this.scriptValidator.ValidationExceptions);
                    //    else
                    //        this.logger.Info("Ignoring script error in block: {0}".Format2(chainedBlock.Hash));
                    //}

                    // commit the chain state
                    this.CommitTransaction();

                    // MONITOR: CommitBlock
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.CommitBlock(chainedBlock.ChainedHeader);

                    // MEASURE: Block Rate
                    this.stats.blockRateMeasure.Tick();

                    // blockchain processing statistics
                    this.Stats.blockCount++;
                    this.Stats.txCount += txCount;
                    this.Stats.inputCount += inputCount;

                    var logInterval = TimeSpan.FromSeconds(15);
                    if (DateTime.UtcNow - this.Stats.lastLogTime >= logInterval)
                    {
                        this.LogBlockchainProgress();
                        this.Stats.lastLogTime = DateTime.UtcNow;
                    }
                }
                catch (Exception)
                {
                    this.RollbackTransaction();

                    // MONITOR: RollbackBlock
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.RollbackBlock(chainedBlock.ChainedHeader);

                    //this.chainStateMonitor.CompleteAdding();
                    //this.chainStateMonitor.WaitToComplete();
                    //this.scriptValidator.CompleteAdding();
                    //this.scriptValidator.WaitToComplete();

                    throw;
                }
            }
        }

        public void RollbackBlock(ChainedBlock chainedBlock)
        {
            //using (this.chainStateMonitor.Start())
            {
                this.BeginTransaction();
                try
                {
                    // MONITOR: BeginBlock
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.BeginBlock(chainedBlock.ChainedHeader);

                    // remove the block from the chain
                    this.Chain.RemoveBlock(chainedBlock.ChainedHeader);

                    // store the block hash
                    this.chainStateBuilderStorage.BlockHeight = this.Chain.Height;
                    this.chainStateBuilderStorage.BlockHash = this.Chain.LastBlockHash;

                    // rollback the utxo
                    this.RollbackUtxo(chainedBlock);

                    // wait for monitor events to finish
                    //this.chainStateMonitor.CompleteAdding();
                    //this.chainStateMonitor.WaitToComplete();

                    // commit the chain state
                    this.CommitTransaction();

                    // MONITOR: CommitBlock
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.CommitBlock(chainedBlock.ChainedHeader);
                }
                catch (Exception)
                {
                    this.RollbackTransaction();

                    //this.chainStateMonitor.CompleteAdding();
                    //this.chainStateMonitor.WaitToComplete();

                    // MONITOR: RollbackBlock
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.RollbackBlock(chainedBlock.ChainedHeader);

                    throw;
                }
            }
        }

        public void LogBlockchainProgress()
        {
            var elapsedSeconds = this.Stats.durationStopwatch.ElapsedSecondsFloat();
            var blockRate = this.stats.blockRateMeasure.GetAverage(TimeSpan.FromSeconds(1));
            var txRate = this.stats.txRateMeasure.GetAverage(TimeSpan.FromSeconds(1));
            var inputRate = this.stats.inputRateMeasure.GetAverage(TimeSpan.FromSeconds(1));

            this.logger.Info(
                string.Join("\n",
                    new string('-', 200),
                    "Height: {0,10} | Duration: {1} /*| Validation: {2} */| Blocks/s: {3,7} | Tx/s: {4,7} | Inputs/s: {5,7} | Processed Tx: {6,7} | Processed Inputs: {7,7} | Utxo Size: {8,7}",
                    new string('-', 200)
                )
                .Format2
                (
                /*0*/ this.Height.ToString("#,##0"),
                /*1*/ this.Stats.durationStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*2*/ this.Stats.validateStopwatch.Elapsed.ToString(@"hh\:mm\:ss"),
                /*3*/ blockRate.ToString("#,##0"),
                /*4*/ txRate.ToString("#,##0"),
                /*5*/ inputRate.ToString("#,##0"),
                /*6*/ this.Stats.txCount.ToString("#,##0"),
                /*7*/ this.Stats.inputCount.ToString("#,##0"),
                /*8*/ this.TransactionCount.ToString("#,##0")
                ));
        }

        public int TransactionCount
        {
            get { return this.chainStateBuilderStorage.TransactionCount; }
        }

        //public int OutputCount
        //{
        //    get { return this.chainStateBuilderStorage.OutputCount; }
        //}

        public bool TryGetUnspentTx(TxOutputKey txOutputKey, out UnspentTx unspentTx)
        {
            return this.chainStateBuilderStorage.TryGetTransaction(txOutputKey.TxHash, out unspentTx);
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            UnspentTx unspentTx;
            if (this.TryGetUnspentTx(txOutputKey, out unspentTx))
            {
                var blockIndex = this.chain.Blocks[unspentTx.BlockIndex].Hash;

                Transaction transaction;
                if (this.blockCache.TryGetTransaction(/*unspentTx.ConfirmedBlockHash,*/ blockIndex, unspentTx.TxIndex, out transaction))
                {
                    txOutput = transaction.Outputs[txOutputKey.TxOutputIndex.ToIntChecked()];
                    return true;
                }
                else
                {
                    txOutput = default(TxOutput);
                    return false;
                }
            }
            else
            {
                txOutput = default(TxOutput);
                return false;
            }
        }

        //public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> GetUnspentOutputs()
        //{
        //    return this.chainStateBuilderStorage.UnspentOutputs();
        //}

        private void CalculateUtxo(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes, out long txCount, out long inputCount)
        {
            // don't include genesis block coinbase in utxo
            if (chainedHeader.Height <= 0)
                throw new InvalidOperationException();

            //using (var txInputQueue = new ProducerConsumer<Tuple<Transaction, int, TxInput, int, TxOutput>>())
            //using (var validateScriptsTask = Task.Factory.StartNew(() => this.ValidateTransactionScripts(chainedBlock, txInputQueue)))
            //using (var txOutputQueue = new ProducerConsumer<Tuple<int, ChainPosition, TxOutput>>())
            //using (var scannerTask = Task.Factory.StartNew(() => this.ScanTransactions(txOutputQueue, monitors)))
            //try
            //{

            txCount = 0;
            inputCount = 0;

            var txIndex = -1;
            foreach (var blockTx in blockTxes)
            {
                txIndex++;
                txCount++;

                if (txIndex == 0)
                {
                    //TODO apply real coinbase rule
                    // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                    var coinbaseTx = blockTx.Transaction;

                    // MONITOR: BeforeAddTransaction
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.BeforeAddTransaction(ChainPosition.Fake(), coinbaseTx);

                    // MONITOR: CoinbaseInput
                    //if (this.chainStateMonitor != null)
                    //    foreach (var input in coinbaseTx.Inputs)
                    //        this.chainStateMonitor.CoinbaseInput(ChainPosition.Fake(), input);

                    this.Mint(coinbaseTx, 0, chainedHeader, isCoinbase: true);

                    // MONITOR: AfterAddTransaction
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.AfterAddTransaction(ChainPosition.Fake(), coinbaseTx);
                }
                else
                {
                    // check for double spends
                    var tx = blockTx.Transaction;

                    // MONITOR: BeforeAddTransaction
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.BeforeAddTransaction(ChainPosition.Fake(), tx);

                    for (var inputIndex = 0; inputIndex < tx.Inputs.Length; inputIndex++)
                    {
                        var input = tx.Inputs[inputIndex];
                        inputCount++;

                        this.Spend(txIndex, tx, inputIndex, input, chainedHeader);

                        // MEASURE: Input Rate
                        this.stats.inputRateMeasure.Tick();
                    }

                    this.Mint(tx, txIndex, chainedHeader, isCoinbase: false);

                    // MONITOR: AfterAddTransaction
                    //if (this.chainStateMonitor != null)
                    //    this.chainStateMonitor.AfterAddTransaction(ChainPosition.Fake(), tx);

                    // MEASURE: Transaction Rate
                    this.stats.txRateMeasure.Tick();
                }
            }

            //}
            //finally
            //{
            //    // ensure that started tasks always complete using a finally block
            //    // any exceptions will be propagated by Task.WaitAll()
            //    //TODO unwrap aggregation exception into something more specific
            //    txInputQueue.CompleteAdding();
            //    txOutputQueue.CompleteAdding();
            //    Task.WaitAll(validateScriptsTask, scannerTask);
            //}
        }

        public void Mint(Transaction tx, int txIndex, ChainedHeader chainedHeader, bool isCoinbase)
        {
            // there exist two duplicate coinbases in the blockchain, which the design assumes to be impossible
            // ignore the first occurrences of these duplicates so that they do not need to later be deleted from the utxo, an unsupported operation
            // no other duplicates will occur again, it is now disallowed
            if ((chainedHeader.Height == DUPE_COINBASE_1_HEIGHT && tx.Hash == DUPE_COINBASE_1_HASH)
                || (chainedHeader.Height == DUPE_COINBASE_2_HEIGHT && tx.Hash == DUPE_COINBASE_2_HASH))
            {
                return;
            }

            // verify transaction does not already exist in utxo
            if (this.chainStateBuilderStorage.ContainsTransaction(tx.Hash))
            {
                // duplicate transaction output
                this.logger.Warn("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(chainedHeader.Height, chainedHeader.Hash.ToHexNumberString()));
                throw new ValidationException(chainedHeader.Hash);
            }

            // add transaction to the utxo
            this.chainStateBuilderStorage.AddTransaction(tx.Hash, new UnspentTx(/*chainedHeader.Hash,*/ chainedHeader.Height, txIndex, tx.Outputs.Length, OutputState.Unspent));

            //// add transaction outputs to the utxo
            //foreach (var output in tx.Outputs.Select((x, i) => new KeyValuePair<TxOutputKey, TxOutput>(new TxOutputKey(tx.Hash, (UInt32)i), x)))
            //{
            //    this.chainStateBuilderStorage.AddOutput(output.Key, output.Value);

            //    // MONITOR: MintTxOutput
            //    if (this.chainStateMonitor != null)
            //        this.chainStateMonitor.MintTxOutput(ChainPosition.Fake(), output.Key, output.Value, GetOutputScripHash(output.Value), isCoinbase);
            //}
        }

        public void Spend(int txIndex, Transaction tx, int inputIndex, TxInput input, ChainedHeader chainedHeader)
        {
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            //|| !this.chainStateBuilderStorage.ContainsOutput(input.PreviousTxOutputKey))
            {
                // output wasn't present in utxo, invalid block
                throw new ValidationException(chainedHeader.Hash);
            }

            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);

            if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
            {
                // output was out of bounds
                throw new ValidationException(chainedHeader.Hash);
            }

            if (unspentTx.OutputStates[outputIndex] == OutputState.Spent)
            {
                // output was already spent
                throw new ValidationException(chainedHeader.Hash);
            }

            //TODO don't remove data immediately, needs to stick around for rollback

            // retrieve previous output
            //TxOutput prevOutput;
            //if (!this.TryGetOutput(input.PreviousTxOutputKey, out prevOutput))
            //    throw new Exception("TODO - corruption");

            // update output states
            unspentTx = unspentTx.SetOutputState(outputIndex, OutputState.Spent);

            // update partially spent transaction in the utxo
            if (unspentTx.OutputStates.Any(x => x == OutputState.Unspent))
            {
                this.chainStateBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx);
            }
            // remove fully spent transaction from the utxo
            else
            {
                this.chainStateBuilderStorage.RemoveTransaction(input.PreviousTxOutputKey.TxHash);

                // store rollback information, the block containing the previous transaction will need to be known during rollback
                //this.spentTransactions.Add(new KeyValuePair<UInt256, SpentTx>(input.PreviousTxOutputKey.TxHash, unspentTx.ToSpent()));
            }

            // store rollback information, the output will need to be added back during rollback
            //this.spentOutputs.Add(new KeyValuePair<TxOutputKey, TxOutput>(input.PreviousTxOutputKey, prevOutput));

            // remove the output from the utxo
            //this.chainStateBuilderStorage.RemoveOutput(input.PreviousTxOutputKey);

            // MONITOR: SpendTxOutput
            //if (this.chainStateMonitor != null)
            //    this.chainStateMonitor.SpendTxOutput(new ChainPosition(chainedHeader.Hash, txIndex, tx.Hash, inputIndex, -1), chainedHeader, tx, input, input.PreviousTxOutputKey, prevOutput, GetOutputScripHash(prevOutput));
        }

        //TODO with the rollback information that's now being stored, rollback could be down without needing the block
        private void RollbackUtxo(Block block)
        {
            //TODO currently a MissingDataException will get thrown if the rollback information is missing
            //TODO rollback is still possible if any resurrecting transactions can be found
            //TODO the network does not allow arbitrary transaction lookup, but if the transactions can be retrieved then this code should allow it
            //TODO this should be handled by a distinct worker that rebuilds rollback information

            //var spentTransactions = new Dictionary<UInt256, SpentTx>();
            //spentTransactions.AddRange(this.spentTransactionsCache[block.Hash]);

            //var spentOutputs = new Dictionary<TxOutputKey, TxOutput>();
            //spentOutputs.AddRange(this.spentOutputsCache[block.Hash]);

            //for (var txIndex = block.Transactions.Count - 1; txIndex >= 1; txIndex--)
            //{
            //    var tx = block.Transactions[txIndex];

            //    // MONITOR: BeforeRemoveTransaction
            //    if (this.chainStateMonitor != null)
            //        this.chainStateMonitor.BeforeRemoveTransaction(ChainPosition.Fake(), tx);

            //    // remove outputs
            //    this.Unmint(tx, this.LastBlock, isCoinbase: false);

            //    // remove inputs in reverse order
            //    for (var inputIndex = tx.Inputs.Count - 1; inputIndex >= 0; inputIndex--)
            //    {
            //        var input = tx.Inputs[inputIndex];
            //        this.Unspend(input, this.LastBlock, spentTransactions, spentOutputs);
            //    }

            //    // MONITOR: AfterRemoveTransaction
            //    if (this.chainStateMonitor != null)
            //        this.chainStateMonitor.AfterRemoveTransaction(ChainPosition.Fake(), tx);
            //}

            //var coinbaseTx = block.Transactions[0];

            //// MONITOR: BeforeRemoveTransaction
            //if (this.chainStateMonitor != null)
            //    this.chainStateMonitor.BeforeRemoveTransaction(ChainPosition.Fake(), coinbaseTx);

            //// remove coinbase outputs
            //this.Unmint(coinbaseTx, this.LastBlock, isCoinbase: true);

            //for (var inputIndex = coinbaseTx.Inputs.Count - 1; inputIndex >= 0; inputIndex--)
            //{
            //    // MONITOR: UnCoinbaseInput
            //    if (this.chainStateMonitor != null)
            //        this.chainStateMonitor.UnCoinbaseInput(ChainPosition.Fake(), coinbaseTx.Inputs[inputIndex]);
            //}

            //// MONITOR: AfterRemoveTransaction
            //if (this.chainStateMonitor != null)
            //    this.chainStateMonitor.AfterRemoveTransaction(ChainPosition.Fake(), coinbaseTx);
        }

        public void Unmint(Transaction tx, ChainedHeader chainedHeader, bool isCoinbase)
        {
            // ignore duplicate coinbases
            if ((chainedHeader.Height == DUPE_COINBASE_1_HEIGHT && tx.Hash == DUPE_COINBASE_1_HASH)
                || (chainedHeader.Height == DUPE_COINBASE_2_HEIGHT && tx.Hash == DUPE_COINBASE_2_HASH))
            {
                return;
            }

            // check that transaction exists
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(tx.Hash, out unspentTx))
            {
                // missing transaction output
                this.logger.Warn("Missing transaction at block {0:#,##0}, {1}, tx {2}".Format2(chainedHeader.Height, chainedHeader.Hash.ToHexNumberString(), tx.Hash));
                throw new ValidationException(chainedHeader.Hash);
            }

            //TODO verify blockheight

            // verify all outputs are unspent before unminting
            if (!unspentTx.OutputStates.All(x => x == OutputState.Unspent))
            {
                throw new ValidationException(chainedHeader.Hash);
            }

            // remove the transaction
            this.chainStateBuilderStorage.RemoveTransaction(tx.Hash);

            // remove the transaction outputs
            for (var outputIndex = 0; outputIndex < tx.Outputs.Length; outputIndex++)
            {
                var txOutput = tx.Outputs[outputIndex];
                var txOutputKey = new TxOutputKey(tx.Hash, (UInt32)outputIndex);

                //this.chainStateBuilderStorage.RemoveOutput(txOutputKey);

                // MONITOR: UnspendTxOutput
                //if (this.chainStateMonitor != null)
                //    this.chainStateMonitor.UnmintTxOutput(ChainPosition.Fake(), txOutputKey, txOutput, GetOutputScripHash(txOutput), isCoinbase);
            }
        }

        public void Unspend(TxInput input, ChainedHeader chainedHeader, Dictionary<UInt256, SpentTx> spentTransactions, Dictionary<TxOutputKey, TxOutput> spentOutputs)
        {
            //TODO currently a MissingDataException will get thrown if the rollback information is missing
            //TODO rollback is still possible if any resurrecting transactions can be found
            //TODO the network does not allow arbitrary transaction lookup, but if the transactions can be retrieved then this code should allow it

            //// retrieve rollback information
            //UInt256 prevTxBlockHash;
            //if (!spentTransactions.TryGetValue(input.PreviousTxOutputKey.TxHash, out prevTxBlockHash))
            //{
            //    //TODO throw should indicate rollback info is missing
            //    throw new MissingDataException(null);
            //}

            // retrieve previous output
            TxOutput prevTxOutput;
            if (!spentOutputs.TryGetValue(input.PreviousTxOutputKey, out prevTxOutput))
                throw new Exception("TODO - corruption");

            // retrieve transaction output states, if not found then a fully spent transaction is being resurrected
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            {
                // retrieve spent transaction
                SpentTx prevSpentTx;
                if (!spentTransactions.TryGetValue(input.PreviousTxOutputKey.TxHash, out prevSpentTx))
                    throw new Exception("TODO - corruption");

                // create fully spent transaction output state
                unspentTx = new UnspentTx(/*prevSpentTx.ConfirmedBlockHash,*/ prevSpentTx.BlockIndex, prevSpentTx.TxIndex, prevSpentTx.OutputCount, OutputState.Spent);
            }

            // retrieve previous output index
            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);
            if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
                throw new Exception("TODO - corruption");

            // check that output isn't already considered unspent
            if (unspentTx.OutputStates[outputIndex] == OutputState.Unspent)
                throw new ValidationException(chainedHeader.Hash);

            // mark output as unspent
            this.chainStateBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx.SetOutputState(outputIndex, OutputState.Unspent));

            // add transaction output back to utxo
            //this.chainStateBuilderStorage.AddOutput(input.PreviousTxOutputKey, prevTxOutput);

            // MONITOR: UnspendTxOutput
            //if (this.chainStateMonitor != null)
            //    this.chainStateMonitor.UnspendTxOutput(ChainPosition.Fake(), input, input.PreviousTxOutputKey, prevTxOutput, GetOutputScripHash(prevTxOutput));
        }

        private UInt256 GetOutputScripHash(TxOutput txOutput)
        {
            return new UInt256(this.sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()));
        }

        private void SaveRollbackInformation(UInt256 blockHash, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
        {
            //spentTransactionsCache[blockHash] = this.spentTransactions.ToImmutable();
            //this.spentTransactions.Clear();

            //spentOutputsCache[blockHash] = this.spentOutputs.ToImmutable();
            //this.spentOutputs.Clear();
        }

        public void Flush()
        {
            this.chainStateBuilderStorage.Flush();
        }

        public ChainState ToImmutable()
        {
            return this.commitLock.DoRead(() =>
                new ChainState(this.chain.ToImmutable(), new Utxo(chainStateBuilderStorage.ToImmutable())));
        }

        private void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            this.commitLock.EnterWriteLock();
            this.chainStateBuilderStorage.BeginTransaction();
            this.savedChain = this.chain.ToImmutable();
            this.inTransaction = true;
        }

        private void CommitTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateBuilderStorage.CommitTransaction();
            this.savedChain = null;
            this.inTransaction = false;
            this.commitLock.ExitWriteLock();
        }

        private void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            this.chainStateBuilderStorage.RollbackTransaction();
            this.chain = this.savedChain.ToBuilder();
            this.inTransaction = false;
            this.commitLock.ExitWriteLock();
        }

        public sealed class BuilderStats : IDisposable
        {
            public Stopwatch durationStopwatch = Stopwatch.StartNew();
            public Stopwatch validateStopwatch = new Stopwatch();

            public long blockCount;
            public long txCount;
            public long inputCount;

            public readonly RateMeasure blockRateMeasure = new RateMeasure();
            public readonly RateMeasure txRateMeasure = new RateMeasure();
            public readonly RateMeasure inputRateMeasure = new RateMeasure();

            public DateTime lastLogTime = DateTime.UtcNow;

            internal BuilderStats() { }

            public void Dispose()
            {
                this.blockRateMeasure.Dispose();
                this.txRateMeasure.Dispose();
                this.inputRateMeasure.Dispose();
            }
        }
    }
}