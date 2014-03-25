using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IUtxoStorage : IDisposable
    {
        UInt256 BlockHash { get; }

        int Count { get; }

        IEnumerable<UnspentTx> UnspentTransactions();

        bool ContainsKey(UInt256 txHash);

        UnspentTx this[UInt256 txHash] { get; }

        void DisposeDelete();
    }
}
