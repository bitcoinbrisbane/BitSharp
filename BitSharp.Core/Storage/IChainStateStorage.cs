using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IChainStateStorage : IDisposable
    {
        int BlockHeight { get; }

        UInt256 BlockHash { get; }

        
        int TransactionCount { get; }

        bool ContainsTransaction(UInt256 txHash);

        bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx);

        IEnumerable<KeyValuePair<UInt256, UnspentTx>> UnspentTransactions();
    }
}
