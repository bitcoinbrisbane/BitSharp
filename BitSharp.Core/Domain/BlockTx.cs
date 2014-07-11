using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class BlockTx : BlockElement
    {
        private readonly Transaction transaction;

        public BlockTx(int index, int depth, UInt256 hash, bool pruned, Transaction transaction)
            : base(index, depth, hash, pruned)
        {
            this.transaction = transaction;
        }

        public Transaction Transaction { get { return this.transaction; } }
    }
}
