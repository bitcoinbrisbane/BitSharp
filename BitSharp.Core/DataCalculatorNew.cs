using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core
{
    //TODO organize and name properly
    public static class DataCalculatorNew
    {
        public static void PruneNode(IMerkleWalker merkleWalker)
        {

        }

        public static IEnumerable<T> ReadMerkleTreeNodes<T>(UInt256 merkleRoot, IEnumerable<T> merkleTreeNodes)
            where T : MerkleTreeNode
        {
            var expectedIndex = 0;

            var merkleStream = new MerkleStream();

            foreach (var node in merkleTreeNodes)
            {
                if (node.Index != expectedIndex)
                {
                    throw new ValidationException(0 /*TODO blockHash*/);
                }

                merkleStream.AddNode(node);

                yield return node;

                expectedIndex += 1 << node.Depth;
            }

            merkleStream.FinishPairing();

            if (merkleStream.RootNode.Hash != merkleRoot)
            {
                throw new ValidationException(0 /*TODO blockHash*/);
            }
        }

        public static UInt256 PairHashes(UInt256 left, UInt256 right)
        {
            var bytes = ImmutableArray.CreateBuilder<byte>(64);
            bytes.AddRange(left.ToByteArray());
            bytes.AddRange(right.ToByteArray());
            return new UInt256(new SHA256Managed().ComputeDoubleHash(bytes.ToArray()));
        }

        private class MerkleStream
        {
            private readonly SHA256Managed sha256 = new SHA256Managed();
            private readonly List<MerkleTreeNode> leftNodes = new List<MerkleTreeNode>();

            public MerkleTreeNode RootNode
            {
                get
                {
                    if (this.leftNodes.Count != 1)
                        throw new InvalidOperationException();

                    return this.leftNodes[0];
                }
            }

            public void AddNode(MerkleTreeNode newNode)
            {
                if (this.leftNodes.Count == 0)
                {
                    this.leftNodes.Add(newNode);
                }
                else
                {
                    var leftNode = this.leftNodes.Last();

                    if (newNode.Depth < leftNode.Depth)
                    {
                        this.leftNodes.Add(newNode);
                    }
                    else if (newNode.Depth == leftNode.Depth)
                    {
                        this.leftNodes[this.leftNodes.Count - 1] = Pair(leftNode, newNode);
                    }
                    else if (newNode.Depth > leftNode.Depth)
                    {
                        throw new InvalidOperationException();
                    }

                    this.ClosePairs();
                }
            }

            public void FinishPairing()
            {
                if (this.leftNodes.Count == 0)
                    throw new InvalidOperationException();

                while (this.leftNodes.Count > 1)
                {
                    var leftNode = this.leftNodes.Last();
                    var rightNode = new MerkleTreeNode(leftNode.Index + (1 << leftNode.Depth), leftNode.Depth, leftNode.Hash);
                    AddNode(rightNode);
                }
            }

            private void ClosePairs()
            {
                while (this.leftNodes.Count >= 2)
                {
                    var leftNode = this.leftNodes[this.leftNodes.Count - 2];
                    var rightNode = this.leftNodes[this.leftNodes.Count - 1];

                    if (leftNode.Depth == rightNode.Depth)
                    {
                        this.leftNodes.RemoveAt(this.leftNodes.Count - 1);
                        this.leftNodes[this.leftNodes.Count - 1] = Pair(leftNode, rightNode);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            private MerkleTreeNode Pair(MerkleTreeNode left, MerkleTreeNode right)
            {
                if (left.Depth != right.Depth)
                    throw new InvalidOperationException();

                var expectedIndex = left.Index + (1 << left.Depth);
                if (right.Index != expectedIndex)
                    throw new InvalidOperationException();

                var pairHashBytes = new byte[64];
                left.Hash.ToByteArray(pairHashBytes, 0);
                right.Hash.ToByteArray(pairHashBytes, 32);

                var pairHash = new UInt256(this.sha256.ComputeDoubleHash(pairHashBytes));

                return new MerkleTreeNode(left.Index, left.Depth + 1, pairHash);
            }


            private UInt256 PairHashes(UInt256 left, UInt256 right)
            {
                var pairHashBytes = new byte[64];
                left.ToByteArray(pairHashBytes, 0);
                right.ToByteArray(pairHashBytes, 32);

                return new UInt256(new SHA256Managed().ComputeDoubleHash(pairHashBytes));
            }
        }
    }
}
