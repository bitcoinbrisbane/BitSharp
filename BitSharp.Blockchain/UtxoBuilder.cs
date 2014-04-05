using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using Ninject;
using Ninject.Parameters;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    //TODO bundle Utxo and ChainState together into ChainState? make Chain persisted along with this change, and then cache it
    //TODO eventually the UtxoBuilder should have an open/close mechanism
    public class UtxoBuilder : IDisposable
    {
        private readonly Logger logger;
        private readonly IUtxoBuilderStorage utxoBuilderStorage;
        private readonly TransactionCache transactionCache;

        //TODO when written more directly against Esent, these can be streamed out so an entire list doesn't need to be held in memory
        private readonly ImmutableList<KeyValuePair<UInt256, SpentTx>>.Builder spentTransactions;
        private readonly ImmutableList<KeyValuePair<TxOutputKey, TxOutput>>.Builder spentOutputs;

        public UtxoBuilder(Utxo parentUtxo, Logger logger, IKernel kernel, TransactionCache transactionCache)
        {
            this.logger = logger;
            this.utxoBuilderStorage = kernel.Get<IUtxoBuilderStorage>(new ConstructorArgument("parentUtxo", parentUtxo.Storage));
            this.transactionCache = transactionCache;

            this.spentTransactions = ImmutableList.CreateBuilder<KeyValuePair<UInt256, SpentTx>>();
            this.spentOutputs = ImmutableList.CreateBuilder<KeyValuePair<TxOutputKey, TxOutput>>();
        }

        ~UtxoBuilder()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            this.utxoBuilderStorage.Dispose();
            GC.SuppressFinalize(this);
        }

        public int TransactionCount
        {
            get { return this.utxoBuilderStorage.TransactionCount; }
        }

        public int OutputCount
        {
            get { return this.utxoBuilderStorage.OutputCount; }
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            return this.utxoBuilderStorage.TryGetOutput(txOutputKey, out txOutput);
        }

        public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> GetUnspentOutputs()
        {
            return this.utxoBuilderStorage.UnspentOutputs();
        }

        public void Mint(Transaction tx, ChainedBlock block)
        {
            // verify transaction does not already exist in utxo
            if (this.utxoBuilderStorage.ContainsTransaction(tx.Hash))
            {
                // two specific duplicates are allowed, from before duplicates were disallowed
                if ((block.Height == 91842 && tx.Hash == UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber))
                    || (block.Height == 91880 && tx.Hash == UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber)))
                {
                    UnspentTx unspentTx;
                    if (!this.utxoBuilderStorage.TryGetTransaction(tx.Hash, out unspentTx))
                        throw new Exception("TODO");

                    //TODO the inverse needs to be special cased in RollbackUtxo as well
                    for (var i = 0; i < unspentTx.OutputStates.Length; i++)
                    {
                        if (unspentTx.OutputStates[i] == OutputState.Unspent)
                        {
                            var txOutputKey = new TxOutputKey(tx.Hash, (UInt32)i);

                            TxOutput prevOutput;
                            if (!this.utxoBuilderStorage.TryGetOutput(txOutputKey, out prevOutput))
                                throw new Exception("TODO");

                            this.utxoBuilderStorage.RemoveOutput(txOutputKey);

                            // store rollback information, the output will need to be added back during rollback
                            this.spentOutputs.Add(new KeyValuePair<TxOutputKey, TxOutput>(txOutputKey, prevOutput));
                        }
                    }
                    this.utxoBuilderStorage.RemoveTransaction(tx.Hash);

                    // store rollback information, the block containing the previous transaction will need to be known during rollback
                    this.spentTransactions.Add(new KeyValuePair<UInt256, SpentTx>(tx.Hash, unspentTx.ToSpent()));
                }
                else
                {
                    // duplicate transaction output
                    this.logger.Warn("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(block.Height, block.BlockHash.ToHexNumberString()));
                    throw new ValidationException(block.BlockHash);
                }
            }

            // add transaction to the utxo
            this.utxoBuilderStorage.AddTransaction(tx.Hash, new UnspentTx(block.BlockHash, tx.Outputs.Count, OutputState.Unspent));

            // add transaction outputs to the utxo
            foreach (var output in tx.Outputs.Select((x, i) => new KeyValuePair<TxOutputKey, TxOutput>(new TxOutputKey(tx.Hash, (UInt32)i), x)))
                this.utxoBuilderStorage.AddOutput(output.Key, output.Value);
        }

        public void Spend(TxInput input, ChainedBlock block)
        {
            UnspentTx unspentTx;
            if (!this.utxoBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx)
                || !this.utxoBuilderStorage.ContainsOutput(input.PreviousTxOutputKey))
            {
                // output wasn't present in utxo, invalid block
                throw new ValidationException(block.BlockHash);
            }

            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);

            if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
            {
                // output was out of bounds
                throw new ValidationException(block.BlockHash);
            }

            if (unspentTx.OutputStates[outputIndex] == OutputState.Spent)
            {
                // output was already spent
                throw new ValidationException(block.BlockHash);
            }

            // update output states
            unspentTx = unspentTx.SetOutputState(outputIndex, OutputState.Spent);

            //TODO don't remove data immediately, needs to stick around for rollback

            // update partially spent transaction in the utxo
            if (unspentTx.OutputStates.Any(x => x == OutputState.Unspent))
            {
                this.utxoBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx);
            }
            // remove fully spent transaction from the utxo
            else
            {
                this.utxoBuilderStorage.RemoveTransaction(input.PreviousTxOutputKey.TxHash);

                // store rollback information, the block containing the previous transaction will need to be known during rollback
                this.spentTransactions.Add(new KeyValuePair<UInt256, SpentTx>(input.PreviousTxOutputKey.TxHash, unspentTx.ToSpent()));
            }

            // retrieve previous output
            TxOutput prevOutput;
            if (!this.utxoBuilderStorage.TryGetOutput(input.PreviousTxOutputKey, out prevOutput))
                throw new Exception("TODO - corruption");

            // store rollback information, the output will need to be added back during rollback
            this.spentOutputs.Add(new KeyValuePair<TxOutputKey, TxOutput>(input.PreviousTxOutputKey, prevOutput));

            // remove the output from the utxo
            this.utxoBuilderStorage.RemoveOutput(input.PreviousTxOutputKey);
        }

        public void Unmint(Transaction tx, ChainedBlock block)
        {
            // check that transaction exists
            UnspentTx unspentTx;
            if (!this.utxoBuilderStorage.TryGetTransaction(tx.Hash, out unspentTx))
            {
                // missing transaction output
                this.logger.Warn("Missing transaction at block {0:#,##0}, {1}, tx {2}".Format2(block.Height, block.BlockHash.ToHexNumberString(), tx.Hash));
                throw new ValidationException(block.BlockHash);
            }

            //TODO verify blockheight

            // verify all outputs are unspent before unminting
            if (!unspentTx.OutputStates.All(x => x == OutputState.Unspent))
            {
                throw new ValidationException(block.BlockHash);
            }

            // remove the transaction
            this.utxoBuilderStorage.RemoveTransaction(tx.Hash);

            // remove the transaction outputs
            for (var outputIndex = 0U; outputIndex < tx.Outputs.Count; outputIndex++)
                this.utxoBuilderStorage.RemoveOutput(new TxOutputKey(tx.Hash, outputIndex));
        }

        public void Unspend(TxInput input, ChainedBlock block, Dictionary<UInt256, SpentTx> spentTransactions, Dictionary<TxOutputKey, TxOutput> spentOutputs)
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
            if (!this.utxoBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            {
                // retrieve spent transaction
                SpentTx prevSpentTx;
                if (!spentTransactions.TryGetValue(input.PreviousTxOutputKey.TxHash, out prevSpentTx))
                    throw new Exception("TODO - corruption");

                // create fully spent transaction output state
                unspentTx = new UnspentTx(prevSpentTx.ConfirmedBlockHash, prevSpentTx.OutputCount, OutputState.Spent);
            }

            // retrieve previous output index
            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);
            if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
                throw new Exception("TODO - corruption");

            // check that output isn't already considered unspent
            if (unspentTx.OutputStates[outputIndex] == OutputState.Unspent)
                throw new ValidationException(block.BlockHash);

            // mark output as unspent
            this.utxoBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx.SetOutputState(outputIndex, OutputState.Unspent));

            // add transaction output back to utxo
            this.utxoBuilderStorage.AddOutput(input.PreviousTxOutputKey, prevTxOutput);
        }

        public void SaveRollbackInformation(int height, UInt256 blockHash, SpentTransactionsCache spentTransactionsCache, SpentOutputsCache spentOutputsCache)
        {
            spentTransactionsCache[blockHash] = this.spentTransactions.ToImmutable();
            this.spentTransactions.Clear();

            spentOutputsCache[blockHash] = this.spentOutputs.ToImmutable();
            this.spentOutputs.Clear();
        }

        public void Flush()
        {
            this.utxoBuilderStorage.Flush();
        }

        public Utxo ToImmutable(UInt256 blockHash)
        {
            return new Utxo(utxoBuilderStorage.ToImmutable(blockHash));
        }

        public Utxo Close(UInt256 blockHash)
        {
            return new Utxo(utxoBuilderStorage.Close(blockHash));
        }
    }
}
