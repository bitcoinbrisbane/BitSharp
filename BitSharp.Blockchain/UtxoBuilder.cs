using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class UtxoBuilder : IDisposable
    {
        private readonly CacheContext cacheContext;
        private readonly IUtxoBuilderStorage utxoBuilderStorage;

        public UtxoBuilder(CacheContext cacheContext, Utxo currentUtxo)
        {
            this.cacheContext = cacheContext;
            this.utxoBuilderStorage = cacheContext.StorageContext.ToUtxoBuilder(currentUtxo);
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

        public bool ContainsKey(UInt256 txHash)
        {
            return this.utxoBuilderStorage.ContainsKey(txHash);
        }

        public bool Remove(UInt256 txHash)
        {
            return this.utxoBuilderStorage.Remove(txHash);
        }

        public void Clear()
        {
            this.utxoBuilderStorage.Clear();
        }

        public void Add(UInt256 txHash, UnspentTx unspentTx)
        {
            this.utxoBuilderStorage.Add(txHash, unspentTx);
        }

        public int Count
        {
            get { return this.utxoBuilderStorage.Count; }
        }

        public UnspentTx this[UInt256 txHash]
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
