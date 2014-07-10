using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class TxWithPrevOutputs
    {
        private readonly Transaction transaction;
        private readonly int txIndex;
        private readonly ChainedHeader chainedHeader;
        private readonly ImmutableArray<TxOutput> prevTxOutputs;

        public TxWithPrevOutputs(int txIndex, Transaction transaction, ChainedHeader chainedHeader, ImmutableArray<TxOutput> prevTxOutputs)
        {
            this.transaction = transaction;
            this.txIndex = txIndex;
            this.chainedHeader = chainedHeader;
            this.prevTxOutputs = prevTxOutputs;
        }

        public Transaction Transaction { get { return this.transaction; } }

        public int TxIndex { get { return this.txIndex; } }

        public ChainedHeader ChainedHeader { get { return this.chainedHeader; } }

        public ImmutableArray<TxOutput> PrevTxOutputs { get { return this.prevTxOutputs; } }
    }
}
