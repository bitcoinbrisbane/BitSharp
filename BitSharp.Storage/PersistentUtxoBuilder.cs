using BitSharp.Common;
using BitSharp.Data;
using Microsoft.Isam.Esent.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class PersistentUtxoBuilder : UtxoBuilder
    {
        private UInt256 _blockHash;
        private PersistentUInt256ByteDictionary _utxo;

        public PersistentUtxoBuilder(UInt256 blockHash, Utxo utxo)
        {
            this._blockHash = blockHash;

            var path = PersistentUtxo.FolderPath(blockHash);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);

            this._utxo = new PersistentUInt256ByteDictionary(path);
            foreach (var unspentTx in utxo.UnspentTransactions())
            {
                _utxo.Add(unspentTx.TxHash, PersistentUtxo.SerializeUnspentTx(unspentTx));
            }
        }

        public PersistentUtxoBuilder(UInt256 blockHash, PersistentUtxo utxo)
        {
            this._blockHash = blockHash;
            utxo.Duplicate(blockHash);
            this._utxo = new PersistentUInt256ByteDictionary(PersistentUtxo.FolderPath(blockHash));
        }

        public UInt256 BlockHash { get { return this._blockHash; } }

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
            _utxo.Add(txHash, PersistentUtxo.SerializeUnspentTx(unspentTx));
        }

        public int Count { get { return this._utxo.Count; } }

        public UnspentTx this[Common.UInt256 txHash]
        {
            get
            {
                return PersistentUtxo.DeserializeUnspentTx(txHash, _utxo[txHash]);
            }
            set
            {
                _utxo[txHash] = PersistentUtxo.SerializeUnspentTx(value);
            }
        }

        public Utxo ToImmutable()
        {
            return new PersistentUtxo(_blockHash, _utxo);
        }
    }
}
