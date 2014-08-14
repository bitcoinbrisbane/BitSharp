using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent.ChainState
{
    internal class CachedChainStateCursor : IChainStateCursor
    {
        private readonly IChainStateCursor chainStateCursor;
        private readonly Action disposeAction;

        public CachedChainStateCursor(IChainStateCursor chainStateCursor, Action disposeAction)
        {
            this.chainStateCursor = chainStateCursor;
            this.disposeAction = disposeAction;
        }

        public void Dispose()
        {
            this.disposeAction();
        }

        public bool InTransaction
        {
            get { return this.chainStateCursor.InTransaction; }
        }

        public void BeginTransaction()
        {
            this.chainStateCursor.BeginTransaction();
        }

        public void CommitTransaction()
        {
            this.chainStateCursor.CommitTransaction();
        }

        public void RollbackTransaction()
        {
            this.chainStateCursor.RollbackTransaction();
        }

        public IEnumerable<ChainedHeader> ReadChain()
        {
            return this.chainStateCursor.ReadChain();
        }

        public ChainedHeader GetChainTip()
        {
            return this.chainStateCursor.GetChainTip();
        }

        public void AddChainedHeader(ChainedHeader chainedHeader)
        {
            this.chainStateCursor.AddChainedHeader(chainedHeader);
        }

        public void RemoveChainedHeader(ChainedHeader chainedHeader)
        {
            this.chainStateCursor.RemoveChainedHeader(chainedHeader);
        }

        public int UnspentTxCount
        {
            get { return this.chainStateCursor.UnspentTxCount; }
        }

        public bool ContainsUnspentTx(UInt256 txHash)
        {
            return this.chainStateCursor.ContainsUnspentTx(txHash);
        }

        public bool TryGetUnspentTx(UInt256 txHash, out UnspentTx unspentTx)
        {
            return this.chainStateCursor.TryGetUnspentTx(txHash, out unspentTx);
        }

        public bool TryAddUnspentTx(UnspentTx unspentTx)
        {
            return this.chainStateCursor.TryAddUnspentTx(unspentTx);
        }

        public bool TryRemoveUnspentTx(UInt256 txHash)
        {
            return this.chainStateCursor.TryRemoveUnspentTx(txHash);
        }

        public bool TryUpdateUnspentTx(UnspentTx unspentTx)
        {
            return this.chainStateCursor.TryUpdateUnspentTx(unspentTx);
        }

        public IEnumerable<UnspentTx> ReadUnspentTransactions()
        {
            return this.chainStateCursor.ReadUnspentTransactions();
        }

        public bool ContainsBlockSpentTxes(int blockIndex)
        {
            return this.chainStateCursor.ContainsBlockSpentTxes(blockIndex);
        }

        public bool TryGetBlockSpentTxes(int blockIndex, out IImmutableList<SpentTx> spentTxes)
        {
            return this.chainStateCursor.TryGetBlockSpentTxes(blockIndex, out spentTxes);
        }

        public bool TryAddBlockSpentTxes(int blockIndex, IImmutableList<SpentTx> spentTxes)
        {
            return this.chainStateCursor.TryAddBlockSpentTxes(blockIndex, spentTxes);
        }

        public bool TryRemoveBlockSpentTxes(int blockIndex)
        {
            return this.chainStateCursor.TryRemoveBlockSpentTxes(blockIndex);
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            this.chainStateCursor.RemoveSpentTransactionsToHeight(spentBlockIndex);
        }

        public void Defragment()
        {
            this.chainStateCursor.Defragment();
        }
    }
}
