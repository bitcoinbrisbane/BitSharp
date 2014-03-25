using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class MemoryUtxoStorage : IUtxoStorage
    {
        private UInt256 _blockHash;
        private ImmutableDictionary<UInt256, UnspentTx> _utxo;

        public MemoryUtxoStorage(UInt256 blockHash, ImmutableDictionary<UInt256, UnspentTx> utxo)
        {
            this._blockHash = blockHash;
            this._utxo = utxo;
        }

        public ImmutableDictionary<UInt256, UnspentTx> Dictionary { get { return this._utxo; } }

        public UInt256 BlockHash
        {
            get { return this._blockHash; }
        }

        public int Count
        {
            get { return _utxo.Count; }
        }

        public IEnumerable<UnspentTx> UnspentTransactions()
        {
            return this._utxo.Values;
        }

        public bool ContainsKey(UInt256 txHash)
        {
            return this._utxo.ContainsKey(txHash);
        }

        public bool TryGetValue(UInt256 txHash, out UnspentTx unspentTx)
        {
            return this._utxo.TryGetValue(txHash, out unspentTx);
        }

        public void Dispose()
        {
        }

        public void DisposeDelete()
        {
        }
    }
}
