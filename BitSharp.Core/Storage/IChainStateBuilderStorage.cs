using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IChainStateBuilderStorage : IDisposable
    {
        void BeginTransaction();
        
        void CommitTransaction();
        
        void RollbackTransaction();

        Chain Chain { get; }

        void AddChainedHeader(ChainedHeader chainedHeader);

        void RemoveChainedHeader(ChainedHeader chainedHeader);

        int TransactionCount { get; }

        bool ContainsTransaction(UInt256 txHash);

        bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx);

        bool TryAddTransaction(UInt256 txHash, UnspentTx unspentTx);

        bool RemoveTransaction(UInt256 txHash, int spentBlockIndex);

        void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx);

        void PrepareSpentTransactions(int spentBlockIndex);

        IEnumerable<KeyValuePair<UInt256, UnspentTx>> ReadUnspentTransactions();

        IEnumerable<SpentTx> ReadSpentTransactions(int spentBlockIndex);

        void RemoveSpentTransactions(int spentBlockIndex);

        void RemoveSpentTransactionsToHeight(int spentBlockIndex);

        IChainStateStorage ToImmutable();
    }
}
