using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
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
        private readonly IUtxoStorageBuilderStorage utxoBuilderStorage;

        public UtxoBuilder(ICacheContext cacheContext, IUtxoStorage parentUtxo)
        {
            this.cacheContext = cacheContext;
            this.utxoBuilderStorage = cacheContext.ToUtxoBuilder(parentUtxo);
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

        public void Mint(Transaction tx, ChainedBlock block)
        {
            // add the coinbase outputs to the utxo
            var coinbaseUnspentTx = new UnspentTx(tx.Hash, tx.Outputs.Count, OutputState.Unspent);

            // verify transaction does not already exist in utxo
            if (this.ContainsKey(tx.Hash))
            {
                // two specific duplicates are allowed, from before duplicates were disallowed
                if ((block.Height == 91842 && tx.Hash == UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber))
                    || (block.Height == 91880 && tx.Hash == UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber)))
                {
                    //TODO the inverse needs to be special cased in RollbackUtxo as well
                    this.Remove(tx.Hash);
                }
                else
                {
                    // duplicate transaction output
                    //Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(blockHeight, block.Hash.ToHexNumberString()));
                    throw new ValidationException(block.BlockHash);
                }
            }

            // add transaction output to to the utxo
            this.Add(tx.Hash, coinbaseUnspentTx);
        }

        public void Spend(TxInput input, ChainedBlock block)
        {
            if (!this.ContainsKey(input.PreviousTxOutputKey.TxHash))
            {
                // output wasn't present in utxo, invalid block
                throw new ValidationException(block.BlockHash);
            }

            var prevUnspentTx = this[input.PreviousTxOutputKey.TxHash];

            if (input.PreviousTxOutputKey.TxOutputIndex >= prevUnspentTx.OutputStates.Length)
            {
                // output was out of bounds
                throw new ValidationException(block.BlockHash);
            }

            if (prevUnspentTx.OutputStates[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()] == OutputState.Spent)
            {
                // output was already spent
                throw new ValidationException(block.BlockHash);
            }

            // remove the output from the utxo
            this[input.PreviousTxOutputKey.TxHash] = prevUnspentTx.SetOutputState(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), OutputState.Spent);

            // remove fully spent transaction from the utxo
            if (this[input.PreviousTxOutputKey.TxHash].OutputStates.All(x => x == OutputState.Spent))
                this.Remove(input.PreviousTxOutputKey.TxHash);
        }

        public void Unmint(Transaction tx, ChainedBlock block)
        {
            // check that transaction exists
            if (!this.ContainsKey(tx.Hash))
            {
                // missing transaction output
                //Debug.WriteLine("Missing transaction at block {0:#,##0}, {1}, tx {2}, output {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, outputIndex));
                throw new ValidationException(block.BlockHash);
            }

            //TODO verify blockheight

            // verify all outputs are unspent before unminting
            var unspentOutputs = this[tx.Hash];
            if (!unspentOutputs.OutputStates.All(x => x == OutputState.Unspent))
            {
                throw new ValidationException(block.BlockHash);
            }

            // remove the outputs
            this.Remove(tx.Hash);
        }

        public void Unspend(TxInput input, ChainedBlock block)
        {
            // add spent outputs back into the rolled back utxo
            if (this.ContainsKey(input.PreviousTxOutputKey.TxHash))
            {
                var prevUnspentTx = this[input.PreviousTxOutputKey.TxHash];

                // check if output is out of bounds
                if (input.PreviousTxOutputKey.TxOutputIndex >= prevUnspentTx.OutputStates.Length)
                    throw new ValidationException(block.BlockHash);

                // check that output isn't already considered unspent
                if (prevUnspentTx.OutputStates[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()] == OutputState.Unspent)
                    throw new ValidationException(block.BlockHash);

                // mark output as unspent
                this[input.PreviousTxOutputKey.TxHash] = prevUnspentTx.SetOutputState(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), OutputState.Unspent);
            }
            else
            {
                // fully spent transaction being added back in during roll back
                var prevUnspentTx = this.cacheContext.TransactionCache[input.PreviousTxOutputKey.TxHash];

                this[input.PreviousTxOutputKey.TxHash] =
                    new UnspentTx(prevUnspentTx.Hash, prevUnspentTx.Outputs.Count, OutputState.Spent)
                    .SetOutputState(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), OutputState.Unspent);
            }
        }

        private bool ContainsKey(UInt256 txHash)
        {
            return this.utxoBuilderStorage.ContainsKey(txHash);
        }

        private bool Remove(UInt256 txHash)
        {
            return this.utxoBuilderStorage.Remove(txHash);
        }

        private void Clear()
        {
            this.utxoBuilderStorage.Clear();
        }

        private void Add(UInt256 txHash, UnspentTx unspentTx)
        {
            this.utxoBuilderStorage.Add(txHash, unspentTx);
        }

        public int Count
        {
            get { return this.utxoBuilderStorage.Count; }
        }

        private UnspentTx this[UInt256 txHash]
        {
            get { return this.utxoBuilderStorage[txHash]; }
            set { this.utxoBuilderStorage[txHash] = value; }
        }

        public void Flush()
        {
            this.utxoBuilderStorage.Flush();
        }

        public IUtxoStorage Close(UInt256 blockHash)
        {
            return utxoBuilderStorage.Close(blockHash);
        }
    }
}
