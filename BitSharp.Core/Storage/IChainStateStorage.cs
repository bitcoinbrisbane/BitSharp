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
        Chain Chain { get; }

        int TransactionCount { get; }

        bool ContainsTransaction(UInt256 txHash);

        bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx);

        IEnumerable<KeyValuePair<UInt256, UnspentTx>> ReadUnspentTransactions();

        IChainStateBuilderStorage ToBuilder();
    }
}
