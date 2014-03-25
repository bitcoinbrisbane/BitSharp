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

        public int Count
        {
            get { return this.utxoStorage.Count; }
        }

        public bool CanSpend(TxOutputKey prevTxOutput)
        {
            if (prevTxOutput == null)
                throw new ArgumentNullException("prevTxOutput");

            UnspentTx prevTx;
            if (this.utxoStorage.TryGetValue(prevTxOutput.TxHash, out prevTx))
            {
                var prevTxOutputIndex = (int)prevTxOutput.TxOutputIndex;
                if (prevTxOutputIndex < 0 || prevTxOutputIndex >= prevTx.OutputStates.Length)
                    return false;

                return prevTx.OutputStates[prevTxOutputIndex] == OutputState.Unspent;
            }
            else
            {
                return false;
            }
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
