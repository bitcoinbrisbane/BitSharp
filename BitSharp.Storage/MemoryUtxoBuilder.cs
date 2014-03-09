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
        private ImmutableDictionary<UInt256, UnspentTx>.Builder utxo;

        public MemoryUtxoBuilder(Utxo parentUtxo)
        {
            this.utxo = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            foreach (var unspentTx in parentUtxo.UnspentTransactions())
            {
                this.utxo.Add(unspentTx.TxHash, unspentTx);
            }
        }

        public MemoryUtxoBuilder(MemoryUtxo parentUtxo)
        {
            this.utxo = parentUtxo.Dictionary.ToBuilder();
        }

        public bool ContainsKey(Common.UInt256 txHash)
        {
            return this.utxo.ContainsKey(txHash);
        }

        public bool Remove(Common.UInt256 txHash)
        {
            return this.utxo.Remove(txHash);
        }

        public void Clear()
        {
            this.utxo.Clear();
        }

        public void Add(Common.UInt256 txHash, UnspentTx unspentTx)
        {
            this.utxo.Add(txHash, unspentTx);
        }

        public int Count { get { return this.utxo.Count; } }

        public UnspentTx this[Common.UInt256 txHash]
        {
            get
            {
                return utxo[txHash];
            }
            set
            {
                utxo[txHash] = value;
            }
        }

        public Utxo Close(UInt256 blockHash)
        {
            return new MemoryUtxo(blockHash, this.utxo.ToImmutable());
        }

        public void Dispose()
        {
        }
    }
}
