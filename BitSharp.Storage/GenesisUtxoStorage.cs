using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class GenesisUtxoStorage : IUtxoStorage
    {
        // genesis block coinbase is not included in utxo, it is unspendable

        private readonly UInt256 blockHash;

        public GenesisUtxoStorage(UInt256 blockHash)
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

        public bool TryGetValue(UInt256 txHash, out UnspentTx unspentTx)
        {
            unspentTx = default(UnspentTx);
            return false;
        }

        public void DisposeDelete()
        {
        }

        public void Dispose()
        {
        }
    }
}
