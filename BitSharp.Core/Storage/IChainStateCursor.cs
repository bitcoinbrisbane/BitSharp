using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    /// <summary>
    /// Represents an open cursor/connection into chain state storage.
    /// 
    /// A chain state cursor is for use by a single thread.
    /// </summary>
    public interface IChainStateCursor : IDisposable
    {
        /// <summary>
        /// Begin a new transaction.
        /// </summary>
        void BeginTransaction();

        /// <summary>
        /// Commit the current transaction.
        /// </summary>
        void CommitTransaction();

        /// <summary>
        /// Rollback the current transaction
        /// </summary>
        void RollbackTransaction();

        /// <summary>
        /// Enumerate the chain's headers.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ChainedHeader> ReadChain();

        /// <summary>
        /// Add a new header to the chain.
        /// </summary>
        /// <param name="chainedHeader">The header to add.</param>
        void AddChainedHeader(ChainedHeader chainedHeader);

        /// <summary>
        /// Remove a header from the chain.
        /// </summary>
        /// <param name="chainedHeader">The header to remove.</param>
        void RemoveChainedHeader(ChainedHeader chainedHeader);

        /// <summary>
        /// The current unspent transaction count.
        /// </summary>
        int UnspentTxCount { get; }

        /// <summary>
        /// Determine whether an unspent transaction is present.
        /// </summary>
        /// <param name="txHash">The transaction's hash.</param>
        /// <returns>true if the transaction is present; otherwise, false</returns>
        bool ContainsUnspentTx(UInt256 txHash);

        /// <summary>
        /// Retreive an unspent transaction.
        /// </summary>
        /// <param name="txHash">The transaction's hash.</param>
        /// <param name="unspentTx">Contains the retrieved transaction when successful; otherwise, null.</param>
        /// <returns>true if the transaction was retrieved; otherwise, false</returns>
        bool TryGetUnspentTx(UInt256 txHash, out UnspentTx unspentTx);

        /// <summary>
        /// Add an unspent transaction.
        /// </summary>
        /// <param name="unspentTx">The unspent transaction.</param>
        /// <returns>true if the transaction was added; otherwise, false</returns>
        bool TryAddUnspentTx(UnspentTx unspentTx);

        /// <summary>
        /// Remove an unspent transaction.
        /// </summary>
        /// <param name="txHash">The transaction's hash.</param>
        /// <returns>true if the transaction was removed; otherwise, false</returns>
        bool TryRemoveUnspentTx(UInt256 txHash);

        /// <summary>
        /// Update an unspent transaction.
        /// </summary>
        /// <param name="unspentTx">The unspent transaction.</param>
        /// <returns>true if the transaction was updated; otherwise, false</returns>
        bool TryUpdateUnspentTx(UnspentTx unspentTx);

        //TODO
        void PrepareSpentTransactions(int spentBlockIndex);

        //TODO
        IEnumerable<UnspentTx> ReadUnspentTransactions();

        //TODO
        IEnumerable<SpentTx> ReadSpentTransactions(int spentBlockIndex);

        //TODO
        void AddSpentTransaction(SpentTx spentTx);

        //TODO
        void RemoveSpentTransactions(int spentBlockIndex);

        //TODO
        void RemoveSpentTransactionsToHeight(int spentBlockIndex);

        //TODO
        void Defragment();
    }
}
