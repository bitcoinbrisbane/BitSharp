using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

        public MerkleTreeNode PairWith(MerkleTreeNode right)
        {
            return Pair(this, right);
        }

        public static MerkleTreeNode Pair(MerkleTreeNode left, MerkleTreeNode right)
        {
            if (left.Depth != right.Depth)
                throw new InvalidOperationException();

            var expectedIndex = left.Index + (1 << left.Depth);
            if (right.Index != expectedIndex)
                throw new InvalidOperationException();

            var pairHashBytes = new byte[64];
            left.Hash.ToByteArray(pairHashBytes, 0);
            right.Hash.ToByteArray(pairHashBytes, 32);

            var sha256 = new SHA256Managed();
            var pairHash = new UInt256(sha256.ComputeDoubleHash(pairHashBytes));

            return new MerkleTreeNode(left.Index, left.Depth + 1, pairHash);
        }
    }
}
