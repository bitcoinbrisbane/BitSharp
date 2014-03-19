using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class GenesisUtxo : Utxo
    {
        // genesis block coinbase is not included in utxo, it is unspendable

        private readonly UInt256 blockHash;

        public GenesisUtxo(UInt256 blockHash)
        {
            this.blockHash = blockHash;
        }

        public UInt256 BlockHash
        {
            get { return this.blockHash; }
        }

        public int Count
        {
            get { return 0; }
        }

        public IEnumerable<UnspentTx> UnspentTransactions()
        {
            return Enumerable.Empty<UnspentTx>();
        }

        public bool ContainsKey(UInt256 txHash)
        {
            return false;
        }

        public UnspentTx this[UInt256 txHash]
        {
            get { return null; }
        }

        public void DisposeDelete()
        {
        }

        public void Dispose()
        {
        }
    }
}
