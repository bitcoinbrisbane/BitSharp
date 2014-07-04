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
    public static class DataCalculatorNew
    {
        public static void PruneNode(IBlockElementWalker merkleWalker, int index)
        {
            if (index == 4)
                Debugger.Break();

            BlockElement element;
            if (!merkleWalker.TryMoveToIndex(index, out element))
                throw new InvalidOperationException();

            if (element.Depth == 0 && !element.Pruned)
            {
                element = element.AsPruned();
                merkleWalker.WriteElement(element);
            }

            bool didWork;
            do
            {
                didWork = false;

                if (element.IsLeft)
                {
                    BlockElement rightElement;
                    if (merkleWalker.TryMoveRight(out rightElement))
                    {
                        if (element.Pruned && rightElement.Pruned && element.Depth == rightElement.Depth)
                        {
                            var newElement = element.PairWith(rightElement);
                            merkleWalker.WriteElement(newElement);
                            merkleWalker.DeleteElementToLeft();

                            element = newElement;
                            didWork = true;
                        }
                    }
                    else
                    {
                        if (element.Index != 0 && element.Pruned)
                        {
                            var newElement = element.PairWithSelf();
                            merkleWalker.WriteElement(newElement);

                            element = newElement;
                            didWork = true;
                        }
                    }
                }
                else
                {
                    BlockElement leftElement;
                    if (merkleWalker.TryMoveLeft(out leftElement))
                    {
                        if (element.Pruned && leftElement.Pruned && element.Depth == leftElement.Depth)
                        {
                            var newElement = leftElement.PairWith(element);
                            merkleWalker.WriteElement(newElement);
                            merkleWalker.DeleteElementToRight();

                            element = newElement;
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
