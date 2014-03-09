using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public interface Utxo : IDisposable
    {
        UInt256 BlockHash { get; }

        int Count { get; }

        IEnumerable<UnspentTx> UnspentTransactions();

        UtxoBuilder ToBuilder();

        bool ContainsKey(UInt256 txHash);

        UnspentTx this[UInt256 txHash] { get; }

        void DisposeDelete();
    }
}
