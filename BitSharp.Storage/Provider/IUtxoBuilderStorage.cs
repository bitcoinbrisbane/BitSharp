using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IUtxoBuilderStorage : IDisposable
    {
        bool ContainsKey(UInt256 txHash);

        bool Remove(UInt256 txHash);

        void Clear();

        void Add(UInt256 txHash, UnspentTx unspentTx);

        int Count { get; }

        UnspentTx this[UInt256 txHash] { get; set; }

        void Flush();

        Utxo Close(UInt256 blockHash);
    }
}
