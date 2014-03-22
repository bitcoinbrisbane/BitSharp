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
        private readonly IUtxoBuilderStorage utxoBuilderStorage;

        public UtxoBuilder(ICacheContext cacheContext, Utxo currentUtxo)
        {
            this.cacheContext = cacheContext;
            this.utxoBuilderStorage = cacheContext.ToUtxoBuilder(currentUtxo);
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

        public void Mint(Transaction tx, int blockHeight)
        {
            // add the coinbase outputs to the utxo
            var coinbaseUnspentTx = new UnspentTx(tx.Hash, new ImmutableBitArray(tx.Outputs.Count, true));

            // verify transaction does not already exist in utxo
            if (this.ContainsKey(tx.Hash))
            {
                // two specific duplicates are allowed, from before duplicates were disallowed
                if ((blockHeight == 91842 && tx.Hash == UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber))
                    || (blockHeight == 91880 && tx.Hash == UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber)))
                {
                    //TODO the inverse needs to be special cased in RollbackUtxo as well
                    this.Remove(tx.Hash);
                }
                else
                {
                    // duplicate transaction output
                    //Debug.WriteLine("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(blockHeight, block.Hash.ToHexNumberString()));
                    throw new ValidationException();
                }
            }

            // add transaction output to to the utxo
            this.Add(tx.Hash, coinbaseUnspentTx);
        }

        public void Spend(TxInput input)
        {
            if (!this.ContainsKey(input.PreviousTxOutputKey.TxHash))
            {
                // output wasn't present in utxo, invalid block
                throw new ValidationException();
            }

            var prevUnspentTx = this[input.PreviousTxOutputKey.TxHash];

            if (input.PreviousTxOutputKey.TxOutputIndex >= prevUnspentTx.UnspentOutputs.Length)
            {
                // output was out of bounds
                throw new ValidationException();
            }

            if (!prevUnspentTx.UnspentOutputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()])
            {                        // output was already spent
                throw new ValidationException();
            }


            // remove the output from the utxo
            this[input.PreviousTxOutputKey.TxHash] =
                new UnspentTx(prevUnspentTx.TxHash, prevUnspentTx.UnspentOutputs.Set(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), false));

            // remove fully spent transaction from the utxo
            if (this[input.PreviousTxOutputKey.TxHash].UnspentOutputs.All(x => !x))
                this.Remove(input.PreviousTxOutputKey.TxHash);
        }

        public void Unmint(Transaction tx, int blockHeight)
        {
            // check that transaction exists
            if (!this.ContainsKey(tx.Hash))
            {
                // missing transaction output
                //Debug.WriteLine("Missing transaction at block {0:#,##0}, {1}, tx {2}, output {3}".Format2(blockHeight, block.Hash.ToHexNumberString(), txIndex, outputIndex));
                throw new ValidationException();
            }

            //TODO verify blockheight

            // verify all outputs are unspent before unminting
            var unspentOutputs = this[tx.Hash];
            if (unspentOutputs.UnspentOutputs.Any(x => !x))
            {
                throw new ValidationException();
            }

            // remove the outputs
            this.Remove(tx.Hash);
        }

        public void Unspend(TxInput input)
        {
            // add spent outputs back into the rolled back utxo
            if (this.ContainsKey(input.PreviousTxOutputKey.TxHash))
            {
                var prevUnspentTx = this[input.PreviousTxOutputKey.TxHash];

                // check if output is out of bounds
                if (input.PreviousTxOutputKey.TxOutputIndex >= prevUnspentTx.UnspentOutputs.Length)
                    throw new ValidationException();

                // check that output isn't already considered unspent
                if (prevUnspentTx.UnspentOutputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()])
                    throw new ValidationException();

                // mark output as unspent
                this[input.PreviousTxOutputKey.TxHash] =
                    new UnspentTx(prevUnspentTx.TxHash, prevUnspentTx.UnspentOutputs.Set(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), true));
            }
            else
            {
                // fully spent transaction being added back in during roll back
                var prevUnspentTx = this.cacheContext.TransactionCache[input.PreviousTxOutputKey.TxHash];

                this[input.PreviousTxOutputKey.TxHash] =
                    new UnspentTx(prevUnspentTx.Hash, new ImmutableBitArray(prevUnspentTx.Outputs.Count, false).Set(input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked(), true));
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

        public Utxo Close(UInt256 blockHash)
        {
            return utxoBuilderStorage.Close(blockHash);
        }
    }
}
