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
    public class MemoryUtxoBuilder : UtxoBuilder
    {
        private UInt256 _blockHash;
        private ImmutableDictionary<UInt256, UnspentTx>.Builder _utxo;

        public MemoryUtxoBuilder(UInt256 blockHash, Utxo utxo)
        {
            this._blockHash = blockHash;
            this._utxo = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            foreach (var unspentTx in utxo.UnspentTransactions())
            {
                _utxo.Add(unspentTx.TxHash, unspentTx);
            }
        }

        public MemoryUtxoBuilder(UInt256 blockHash, MemoryUtxo utxo)
        {
            this._blockHash = blockHash;
            this._utxo = utxo.Dictionary.ToBuilder();
        }

        public bool ContainsKey(Common.UInt256 txHash)
        {
            return _utxo.ContainsKey(txHash);
        }

        public bool Remove(Common.UInt256 txHash)
        {
            return _utxo.Remove(txHash);
        }

        public void Add(Common.UInt256 txHash, UnspentTx unspentTx)
        {
            _utxo.Add(txHash, unspentTx);
        }

        public int Count { get { return this._utxo.Count; } }

        public UnspentTx this[Common.UInt256 txHash]
        {
            get
            {
                return _utxo[txHash];
            }
            set
            {
                _utxo[txHash] = value;
            }
        }

        public Utxo ToImmutable()
        {
            return new MemoryUtxo(_blockHash, _utxo.ToImmutable());
        }
    }
}
