using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class BlockElement : MerkleTreeNode
    {
        private readonly UInt256 blockHash;

        public BlockElement(UInt256 blockHash, int index, int depth, UInt256 hash)
            : base(index, depth, hash)
        {
            this.blockHash = blockHash;
        }

        public UInt256 BlockHash { get { return this.blockHash; } }

        public BlockTx ToBlockTx(Transaction transaction)
        {
            return new BlockTx(this.blockHash, this.Index, this.Depth, this.Hash, transaction);
        }
    }
}
