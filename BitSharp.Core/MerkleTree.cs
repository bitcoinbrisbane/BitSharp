using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core
{
    //TODO organize and name properly
    public static class MerkleTree
    {
        public static void PruneNode(IMerkleTreePruningCursor cursor, int index)
        {
            MerkleTreeNode node;
            if (!cursor.TryMoveToIndex(index, out node))
                throw new InvalidOperationException();

            if (node.Depth != 0)
                throw new InvalidOperationException();

            if (!node.Pruned)
            {
                node = node.AsPruned();
                cursor.WriteNode(node);
            }

            bool didWork;
            do
            {
                didWork = false;

                if (node.IsLeft)
                {
                    MerkleTreeNode rightNode;
                    if (cursor.TryMoveRight(out rightNode))
                    {
                        if (node.Pruned && rightNode.Pruned && node.Depth == rightNode.Depth)
                        {
                            var newNode = node.PairWith(rightNode);
                            cursor.MoveLeft();
                            cursor.WriteNode(newNode);
                            cursor.DeleteNodeToRight();

                            node = newNode;
                            didWork = true;
                        }
                    }
                    else
                    {
                        if (node.Index != 0 && node.Pruned)
                        {
                            var newNode = node.PairWithSelf();
                            cursor.WriteNode(newNode);

                            node = newNode;
                            didWork = true;
                        }
                    }
                }
                else
                {
                    MerkleTreeNode leftNode;
                    if (cursor.TryMoveLeft(out leftNode))
                    {
                        if (node.Pruned && leftNode.Pruned && node.Depth == leftNode.Depth)
                        {
                            var newNode = leftNode.PairWith(node);
                            cursor.WriteNode(newNode);
                            cursor.DeleteNodeToRight();

                            node = newNode;
                            didWork = true;
                        }
                    }
                }
            } while (didWork);
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
                    throw new InvalidOperationException();
                }

                merkleStream.AddNode(node);

                yield return node;

                expectedIndex += 1 << node.Depth;
            }

            merkleStream.FinishPairing();

            if (merkleStream.RootNode.Hash != merkleRoot)
            {
                throw new InvalidOperationException();
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
                // when streamining nodes, treat them as being pruned so they can be paired together
                newNode = newNode.AsPruned();

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
                        this.leftNodes[this.leftNodes.Count - 1] = leftNode.PairWith(newNode);
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
                    var rightNode = new MerkleTreeNode(leftNode.Index + (1 << leftNode.Depth), leftNode.Depth, leftNode.Hash, pruned: true);
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
                        this.leftNodes[this.leftNodes.Count - 1] = leftNode.PairWith(rightNode);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }
}
