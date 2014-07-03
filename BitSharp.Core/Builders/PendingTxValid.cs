using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class PendingTxValid
    {
        public readonly Transaction transaction;
        public readonly int txIndex;
        public readonly ChainedHeader chainedHeader;
        public readonly bool isCoinbase;
        public readonly ImmutableArray<TxOutput> prevTxOutputs;

        public PendingTxValid(int txIndex, Transaction transaction, ChainedHeader chainedHeader, bool isCoinbase, ImmutableArray<TxOutput> prevTxOutputs)
        {
            this.transaction = transaction;
            this.txIndex = txIndex;
            this.chainedHeader = chainedHeader;
            this.isCoinbase = isCoinbase;
            this.prevTxOutputs = prevTxOutputs;
        }
    }
}
