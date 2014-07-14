using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage.Memory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    /// <summary>
    /// A pruning cursor which caches reads and defers updates and deletes for a parent pruning cursor.
    /// 
    /// After pruning has been completed, the final list of updates and deletes can be retrieved. This cursor
    /// can be used to prevent intermediate updates to the merkle tree from being written out to storage.
    /// </summary>
    public class DeferredMerkleTreePruningCursor : IMerkleTreePruningCursor
    {
        private readonly IMerkleTreePruningCursor parentCursor;

        private readonly Dictionary<int, MerkleTreeNode> cachedNodes;

        private readonly Dictionary<int, int?> indicesToLeft;
        private readonly Dictionary<int, int?> indicesToRight;

        private readonly HashSet<int> updatedIndices;
        private readonly HashSet<int> deletedIndices;

        private int currentIndex;
        private bool beforeStart;
        private bool afterEnd;

        public DeferredMerkleTreePruningCursor(IMerkleTreePruningCursor parentCursor)
        {
            this.parentCursor = parentCursor;

            this.cachedNodes = new Dictionary<int, MerkleTreeNode>();
            this.indicesToLeft = new Dictionary<int, int?>();
            this.indicesToRight = new Dictionary<int, int?>();

            this.updatedIndices = new HashSet<int>();
            this.deletedIndices = new HashSet<int>();

            this.currentIndex = -1;
        }

        public void Dispose()
        {
        }

        public void BeginTransaction()
        {
            throw new NotSupportedException();
        }

        public void CommitTransaction()
        {
            throw new NotSupportedException();
        }

        public void RollbackTransaction()
        {
            throw new NotSupportedException();
        }

        public bool TryMoveToIndex(int index, out MerkleTreeNode node)
        {
            if (!this.cachedNodes.TryGetValue(index, out node))
            {
                this.parentCursor.TryMoveToIndex(index, out node);
                this.cachedNodes[index] = node;
            }

            this.currentIndex = (node != null ? index : -1);
            this.beforeStart = false;
            this.afterEnd = false;

            return node != null;
        }

        public bool TryMoveLeft(out MerkleTreeNode nodeToLeft)
        {
            if (this.currentIndex == -1)
                throw new InvalidOperationException();
            else if (this.afterEnd)
            {
                this.afterEnd = false;
                nodeToLeft = this.cachedNodes[this.currentIndex];
                return nodeToLeft != null;
            }

            int? indexToLeft;
            if (this.indicesToLeft.TryGetValue(this.currentIndex, out indexToLeft))
            {
                if (indexToLeft != null)
                {
                    if (!this.TryMoveToIndex(indexToLeft.Value, out nodeToLeft))
                        throw new InvalidOperationException();
                }
                else
                {
                    nodeToLeft = null;
                    this.beforeStart = true;
                }
            }
            else
            {
                MerkleTreeNode ignore;
                if (!this.parentCursor.TryMoveToIndex(this.currentIndex, out ignore))
                    throw new InvalidOperationException();

                do
                {
                    this.parentCursor.TryMoveLeft(out nodeToLeft);
                } while (nodeToLeft != null && this.deletedIndices.Contains(nodeToLeft.Index));

                this.indicesToLeft[this.currentIndex] = (nodeToLeft != null ? nodeToLeft.Index : (int?)null);
                if (nodeToLeft != null)
                    this.indicesToRight[nodeToLeft.Index] = this.currentIndex;

                if (nodeToLeft != null)
                    this.currentIndex = nodeToLeft.Index;
                else
                    this.beforeStart = true;
            }

            return nodeToLeft != null;
        }

        public bool TryMoveRight(out MerkleTreeNode nodeToRight)
        {
            if (this.currentIndex == -1)
                throw new InvalidOperationException();
            else if (this.beforeStart)
            {
                if (this.currentIndex != 0)
                    throw new InvalidOperationException();

                this.beforeStart = false;
                nodeToRight = this.cachedNodes[this.currentIndex];
                return nodeToRight != null;
            }

            int? indexToRight;
            if (this.indicesToRight.TryGetValue(this.currentIndex, out indexToRight))
            {
                if (indexToRight != null)
                {
                    if (!this.TryMoveToIndex(indexToRight.Value, out nodeToRight))
                        throw new InvalidOperationException();
                }
                else
                {
                    nodeToRight = null;
                    this.afterEnd = true;
                }
            }
            else
            {
                MerkleTreeNode ignore;
                if (!this.parentCursor.TryMoveToIndex(this.currentIndex, out ignore))
                    throw new InvalidOperationException();

                do
                {
                    this.parentCursor.TryMoveRight(out nodeToRight);
                } while (nodeToRight != null && this.deletedIndices.Contains(nodeToRight.Index));

                this.indicesToRight[this.currentIndex] = (nodeToRight != null ? nodeToRight.Index : (int?)null);
                if (nodeToRight != null)
                    this.indicesToLeft[nodeToRight.Index] = this.currentIndex;

                if (nodeToRight != null)
                    this.currentIndex = nodeToRight.Index;
                else
                    this.afterEnd = true;
            }

            if (nodeToRight == null)
                this.MoveLeft();

            return nodeToRight != null;
        }

        public void WriteNode(MerkleTreeNode node)
        {
            if (this.currentIndex == -1 || this.beforeStart || this.afterEnd)
                throw new InvalidOperationException();
            if (this.currentIndex != node.Index)
                throw new InvalidOperationException();

            this.cachedNodes[node.Index] = node;
            this.updatedIndices.Add(node.Index);
        }

        public void MoveLeft()
        {
            MerkleTreeNode ignore;
            if (!this.TryMoveLeft(out ignore))
                throw new InvalidOperationException();
        }

        public void MoveRight()
        {
            MerkleTreeNode ignore;
            if (!this.TryMoveRight(out ignore))
                throw new InvalidOperationException();
        }

        public void DeleteNodeToRight()
        {
            if (this.currentIndex == -1 || this.beforeStart || this.afterEnd)
                throw new InvalidOperationException();

            var nodeToLeftIndex = this.currentIndex;

            // move right to the node to delete
            this.MoveRight();

            // try to find the next node to the right, so its index is known
            MerkleTreeNode ignore;
            if (this.TryMoveRight(out ignore))
                this.MoveLeft();

            int? nodeToRightIndex;
            if (!this.indicesToRight.TryGetValue(this.currentIndex, out nodeToRightIndex))
                throw new InvalidOperationException();

            this.indicesToRight[nodeToLeftIndex] = nodeToRightIndex;
            if (nodeToRightIndex != null)
                this.indicesToLeft[nodeToRightIndex.Value] = nodeToLeftIndex;

            this.cachedNodes.Remove(this.currentIndex);
            this.indicesToLeft.Remove(this.currentIndex);
            this.indicesToRight.Remove(this.currentIndex);

            this.updatedIndices.Remove(this.currentIndex);
            this.deletedIndices.Add(this.currentIndex);

            this.currentIndex = nodeToLeftIndex;
        }

        public void ApplyChanges()
        {
            foreach (var updatedIndex in this.updatedIndices)
            {
                MerkleTreeNode ignore;
                if (this.parentCursor.TryMoveToIndex(updatedIndex, out ignore))
                {
                    this.parentCursor.WriteNode(this.cachedNodes[updatedIndex]);
                }
            }

            foreach (var deletedIndex in this.deletedIndices)
            {
                MerkleTreeNode ignore;
                if (this.parentCursor.TryMoveToIndex(deletedIndex, out ignore))
                {
                    this.parentCursor.MoveLeft();
                    this.parentCursor.DeleteNodeToRight();
                }
            }
        }

        //TODO remove
        public IEnumerable<MerkleTreeNode> ReadNodes()
        {
            this.ApplyChanges();
            return ((MemoryMerkleTreePruningCursor)parentCursor).ReadNodes();
        }
    }
}
