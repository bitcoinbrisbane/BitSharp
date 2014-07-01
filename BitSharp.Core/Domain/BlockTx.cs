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

        public BlockTx(UInt256 blockHash, int index, int depth, UInt256 hash, Transaction transaction)
            : base(blockHash, index, depth, hash)
        {
            this.transaction = transaction;
        }

        public Transaction Transaction { get { return this.transaction; } }
    }
}
