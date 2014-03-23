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
    public class MemoryUtxoBuilderStorage : IUtxoBuilderStorage
    {
        private ImmutableDictionary<UInt256, UnspentTx>.Builder utxo;

        public MemoryUtxoBuilderStorage(Utxo parentUtxo)
        {
            if (parentUtxo is MemoryUtxo)
            {
                this.utxo = ((MemoryUtxo)parentUtxo).Dictionary.ToBuilder();
            }
            else
            {
                this.utxo = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();
                this.utxo.AddRange(parentUtxo.UnspentTransactions().Select(x => new KeyValuePair<UInt256, UnspentTx>(x.TxHash, x)));
            }
        }

        public ImmutableDictionary<UInt256, UnspentTx>.Builder Storage { get { return this.utxo; } }

        public bool ContainsKey(UInt256 txHash)
        {
            return this.utxo.ContainsKey(txHash);
        }

        public bool Remove(UInt256 txHash)
        {
            return this.utxo.Remove(txHash);
        }

        public void Clear()
        {
            this.utxo.Clear();
        }

        public void Add(UInt256 txHash, UnspentTx unspentTx)
        {
            this.utxo.Add(txHash, unspentTx);
        }

        public int Count { get { return this.utxo.Count; } }

        public UnspentTx this[UInt256 txHash]
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

        public void Flush()
        {
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
