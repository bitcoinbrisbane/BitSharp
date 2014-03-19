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
    public class MemoryUtxo : Utxo
    {
        private UInt256 _blockHash;
        private ImmutableDictionary<UInt256, UnspentTx> _utxo;

        public MemoryUtxo(UInt256 blockHash, ImmutableDictionary<UInt256, UnspentTx> utxo)
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

        public UnspentTx this[UInt256 txHash]
        {
            get { return this._utxo[txHash]; }
        }

        public void Dispose()
        {
        }

        public void DisposeDelete()
        {
        }
    }
}
