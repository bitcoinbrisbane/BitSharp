using BitSharp.Common;
using BitSharp.Data;
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

        public IEnumerable<UnspentTx> UnspentTransactions()
        {
            return this.utxoStorage.UnspentTransactions();
        }

        public bool ContainsKey(UInt256 txHash)
        {
            return this.utxoStorage.ContainsKey(txHash);
        }

        public UnspentTx this[UInt256 txHash]
        {
            get { return this.utxoStorage[txHash]; }
        }

        public void DisposeDelete()
        {
            this.utxoStorage.DisposeDelete();
        }

        public static Utxo CreateForGenesisBlock(ChainedBlock genesisBlock)
        {
            return new Utxo(new GenesisUtxo(genesisBlock.BlockHash));
        }
    }
}
