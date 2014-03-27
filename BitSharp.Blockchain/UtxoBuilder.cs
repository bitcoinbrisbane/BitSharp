using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
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
    public class UtxoBuilder : IDisposable
    {
        private readonly ICacheContext cacheContext;
        private readonly IUtxoBuilderStorage utxoBuilderStorage;
        private ImmutableList<KeyValuePair<UInt256, UInt256>>.Builder blockRollbackInformation;

        public UtxoBuilder(ICacheContext cacheContext, Utxo parentUtxo)
        {
            this.cacheContext = cacheContext;
            this.utxoBuilderStorage = cacheContext.ToUtxoBuilder(parentUtxo.Storage);
            this.blockRollbackInformation = ImmutableList.CreateBuilder<KeyValuePair<UInt256, UInt256>>();
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
                            this.utxoBuilderStorage.RemoveOutput(new TxOutputKey(tx.Hash, (UInt32)i));
                    }
                    this.utxoBuilderStorage.RemoveTransaction(tx.Hash);
                }
                else
                {
                    // duplicate transaction output
                    //Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(blockHeight, block.Hash.ToHexNumberString()));
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
                this.blockRollbackInformation.Add(new KeyValuePair<UInt256, UInt256>(input.PreviousTxOutputKey.TxHash, unspentTx.ConfirmedBlockHash));
            }
            // remove fully spent transaction from the utxo
            else
            {
                this.utxoBuilderStorage.RemoveTransaction(input.PreviousTxOutputKey.TxHash);
            }

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
                //Debug.WriteLine("Missing transaction at block {0:#,##0}, {1}, tx {2}, output {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, outputIndex));
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
        }

        public void Unspend(TxInput input, ChainedBlock block, Dictionary<UInt256, UInt256> blockRollbackDictionary)
        {
            //TODO i need rollback information for a fully spent transaction being resurrected
            //TODO the fully spent transaction could have existed all the way back to the genesis block
            //TODO i need to be able to get the output from the transaction, and the block it ocurred in so i can store confirmation information

            // retrieve rollback information
            UInt256 prevTxBlockHash;
            if (!blockRollbackDictionary.TryGetValue(input.PreviousTxOutputKey.TxHash, out prevTxBlockHash))
            {
                //TODO throw should indicate rollback info is missing
                throw new MissingDataException(null);
            }
            
            // retrieve previous transaction
            Transaction prevTx;
            if (!this.cacheContext.TransactionCache.TryGetValue(input.PreviousTxOutputKey.TxHash, out prevTx))
            {
                // if transaction is missing, use rollback information to determine which block contains it
                throw new MissingDataException(prevTxBlockHash);
            }

            // check if output is out of bounds
            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);
            if (outputIndex < 0 || outputIndex >= prevTx.Outputs.Count)
                throw new ValidationException(block.BlockHash);

            // retrieve previous output
            var prevTxOutput = prevTx.Outputs[outputIndex];

            // retrieve transaction output states, if not found then a fully spent transaction is being resurrected
            UnspentTx unspentTx;
            if (!this.utxoBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            {
                // create fully spent transaction output state
                unspentTx = new UnspentTx(prevTxBlockHash, prevTx.Outputs.Count, OutputState.Spent);
            }

            // double-check for out of bounds
            if (unspentTx.OutputStates.Length != prevTx.Outputs.Count)
                throw new ValidationException(block.BlockHash);

            // check that output isn't already considered unspent
            if (unspentTx.OutputStates[outputIndex] == OutputState.Unspent)
                throw new ValidationException(block.BlockHash);

            // mark output as unspent
            this.utxoBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx.SetOutputState(outputIndex, OutputState.Unspent));

            // add transaction output back to utxo
            this.utxoBuilderStorage.AddOutput(input.PreviousTxOutputKey, prevTxOutput);
        }

        public IImmutableList<KeyValuePair<UInt256, UInt256>> CollectBlockRollbackInformation()
        {
            var info = this.blockRollbackInformation.ToImmutable();
            this.blockRollbackInformation = ImmutableList.CreateBuilder<KeyValuePair<UInt256, UInt256>>();
            return info;
        }

        public void Flush()
        {
            this.utxoBuilderStorage.Flush();
        }

        public Utxo Close(UInt256 blockHash)
        {
            return new Utxo(utxoBuilderStorage.Close(blockHash));
        }
    }
}
