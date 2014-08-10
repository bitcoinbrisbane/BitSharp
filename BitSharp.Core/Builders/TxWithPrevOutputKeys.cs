using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    internal class TxWithPrevOutputKeys
    {
        private readonly Transaction transaction;
        private readonly int txIndex;
        private readonly ChainedHeader chainedHeader;
        private readonly ImmutableArray<BlockTxKey> prevOutputTxKeys;

        public TxWithPrevOutputKeys(int txIndex, Transaction transaction, ChainedHeader chainedHeader, ImmutableArray<BlockTxKey> prevOutputTxKeys)
        {
            this.transaction = transaction;
            this.txIndex = txIndex;
            this.chainedHeader = chainedHeader;
            this.prevOutputTxKeys = prevOutputTxKeys;
        }

        public Transaction Transaction { get { return this.transaction; } }

        public int TxIndex { get { return this.txIndex; } }

        public ChainedHeader ChainedHeader { get { return this.chainedHeader; } }

        public ImmutableArray<BlockTxKey> PrevOutputTxKeys { get { return this.prevOutputTxKeys; } }
    }
}
