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
        private readonly IChainStateStorage chainStateStorage;

        internal Utxo(IChainStateStorage chainStateStorage)
        {
            this.chainStateStorage = chainStateStorage;
        }

        public IChainStateStorage Storage
        {
            get { return this.chainStateStorage; }
        }

        public int TransactionCount
        {
            get { return this.chainStateStorage.TransactionCount; }
        }

        public bool CanSpend(TxOutputKey txOutputKey)
        {
            if (txOutputKey == null)
                throw new ArgumentNullException("prevTxOutput");

            UnspentTx unspentTx;
            if (this.chainStateStorage.TryGetTransaction(txOutputKey.TxHash, out unspentTx))
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

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> GetUnspentTransactions()
        {
            return this.chainStateStorage.ReadUnspentTransactions();
        }

        public static Utxo CreateForGenesisBlock(ChainedHeader genesisHeader)
        {
            return new Utxo(new GenesisChainStateStorage(genesisHeader));
        }
    }
}
