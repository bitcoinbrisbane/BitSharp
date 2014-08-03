using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Domain
{
    public class Utxo
    {
        private readonly IChainStateCursor chainStateCursor;

        internal Utxo(IChainStateCursor chainStateStorage)
        {
            this.chainStateCursor = chainStateStorage;
        }

        public IChainStateCursor Storage
        {
            get { return this.chainStateCursor; }
        }

        public int TransactionCount
        {
            get { return this.chainStateCursor.UnspentTxCount; }
        }

        public bool CanSpend(TxOutputKey txOutputKey)
        {
            if (txOutputKey == null)
                throw new ArgumentNullException("prevTxOutput");

            UnspentTx unspentTx;
            if (this.chainStateCursor.TryGetUnspentTx(txOutputKey.TxHash, out unspentTx))
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

        public IEnumerable<UnspentTx> GetUnspentTransactions()
        {
            return this.chainStateCursor.ReadUnspentTransactions();
        }
    }
}
