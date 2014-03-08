using BitSharp.Common;
using Microsoft.Isam.Esent.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class PersistentUtxoBuilder : UtxoBuilder
    {
        private UInt256 _blockHash;
        private PersistentDictionary<string, string> _utxo;

        public PersistentUtxoBuilder(UInt256 blockHash, Utxo utxo)
        {
            this._blockHash = blockHash;
            
            var path = PersistentUtxo.FolderPath(blockHash);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);

            this._utxo = new PersistentDictionary<string, string>(path);
            foreach (var unspentTx in utxo.UnspentTransactions())
            {
                _utxo.Add(unspentTx.TxHash.ToBigInteger().ToString(), PersistentUtxo.SerializeUnspentTx(unspentTx));
            }
        }

        public PersistentUtxoBuilder(UInt256 blockHash, PersistentUtxo utxo)
        {
            this._blockHash = blockHash;
            utxo.Duplicate(blockHash);
            this._utxo = new PersistentDictionary<string, string>(PersistentUtxo.FolderPath(blockHash));
        }

        public bool ContainsKey(Common.UInt256 txHash)
        {
            return _utxo.ContainsKey(txHash.ToBigInteger().ToString());
        }

        public bool Remove(Common.UInt256 txHash)
        {
            return _utxo.Remove(txHash.ToBigInteger().ToString());
        }

        public void Add(Common.UInt256 txHash, UnspentTx unspentTx)
        {
            _utxo.Add(txHash.ToBigInteger().ToString(), PersistentUtxo.SerializeUnspentTx(unspentTx));
        }

        public UnspentTx this[Common.UInt256 txHash]
        {
            get
            {
                return PersistentUtxo.DeserializeUnspentTx(txHash.ToBigInteger().ToString(), _utxo[txHash.ToBigInteger().ToString()]);
            }
            set
            {
                _utxo[txHash.ToBigInteger().ToString()] = PersistentUtxo.SerializeUnspentTx(value);
            }
        }

        public Utxo ToImmutable()
        {
            return new PersistentUtxo(_blockHash, _utxo);
        }
    }
}
