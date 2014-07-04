using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class MerkleTreeNode
    {
        private readonly int index;
        private readonly int depth;
        private readonly UInt256 hash;

        public MerkleTreeNode(int index, int depth, UInt256 hash)
        {
            this.index = index;
            this.depth = depth;
            this.hash = hash;
        }

        public int Index { get { return this.index; } }

        public int Depth { get { return this.depth; } }

        public UInt256 Hash { get { return this.hash; } }
    }
}
