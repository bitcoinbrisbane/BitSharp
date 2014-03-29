using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class Utxo : IDisposable
    {
        private readonly IUtxoStorage utxoStorage;

        internal Utxo(IUtxoStorage utxoStorage)
        {
            this.utxoStorage = utxoStorage;
        }

        public void Dispose()
        {
            this.utxoStorage.Dispose();
        }

        internal IUtxoStorage Storage
        {
            get { return this.utxoStorage; }
        }

        public UInt256 BlockHash
        {
            get { return this.utxoStorage.BlockHash; }
        }

        public int TransactionCount
        {
            get { return this.utxoStorage.TransactionCount; }
        }

        public int OutputCount
        {
            get { return this.utxoStorage.OutputCount; }
        }

        public bool CanSpend(TxOutputKey txOutputKey)
        {
            if (txOutputKey == null)
                throw new ArgumentNullException("prevTxOutput");

            UnspentTx unspentTx;
            if (this.utxoStorage.TryGetTransaction(txOutputKey.TxHash, out unspentTx))
            {
                var outputIndex = unchecked((int)txOutputKey.TxOutputIndex);
                
                if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
                    return false;

                return unspentTx.OutputStates[outputIndex] == OutputState.Unspent;
            }
            else
            {
                return false;
            }
        }

        public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> GetUnspentOutputs()
        {
            return this.utxoStorage.UnspentOutputs();
        }

        public void DisposeDelete()
        {
            this.utxoStorage.DisposeDelete();
        }

        public static Utxo CreateForGenesisBlock(UInt256 blockHash)
        {
            return new Utxo(new GenesisUtxoStorage(blockHash));
        }
    }
}
