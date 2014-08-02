using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IChainStateCursor : IDisposable
    {
        void BeginTransaction();
        
        void CommitTransaction();
        
        void RollbackTransaction();

        IEnumerable<ChainedHeader> ReadChain();
        
        void AddChainedHeader(ChainedHeader chainedHeader);

        void RemoveChainedHeader(ChainedHeader chainedHeader);

        int TransactionCount { get; }

        bool ContainsTransaction(UInt256 txHash);

        bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx);

        bool TryAddTransaction(UInt256 txHash, UnspentTx unspentTx);

        bool TryRemoveTransaction(UInt256 txHash);

        void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx);

        void PrepareSpentTransactions(int spentBlockIndex);

        IEnumerable<UnspentTx> ReadUnspentTransactions();

        IEnumerable<SpentTx> ReadSpentTransactions(int spentBlockIndex);

        void AddSpentTransaction(SpentTx spentTx);

        void RemoveSpentTransactions(int spentBlockIndex);

        void RemoveSpentTransactionsToHeight(int spentBlockIndex);

        IChainStateStorage ToImmutable();

        void Defragment();
    }
}
