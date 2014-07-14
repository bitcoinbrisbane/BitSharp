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

        public bool TryMoveToIndex(int index)
        {
            MerkleTreeNode node;
            if (!this.cachedNodes.TryGetValue(index, out node))
            {
                this.parentCursor.MoveToIndex(index);
                node = this.parentCursor.ReadNode();
                
                if (node.Index != index)
                    throw new InvalidOperationException();
                this.cachedNodes[index] = node;
            }

            this.currentIndex = (node != null ? index : -1);
            this.beforeStart = false;
            this.afterEnd = false;

            return node != null;
        }

        public bool TryMoveLeft()
        {
            if (this.currentIndex == -1)
                throw new InvalidOperationException();
            else if (this.afterEnd)
            {
                this.afterEnd = false;
                return true;
            }

            int? indexToLeft;
            if (this.indicesToLeft.TryGetValue(this.currentIndex, out indexToLeft))
            {
                if (indexToLeft != null)
                {
                    this.MoveToIndex(indexToLeft.Value);
                    return true;
                }
                else
                {
                    Debug.Assert(this.currentIndex == 0);
                    this.beforeStart = true;
                    return false;
                }
            }
            else
            {
                this.parentCursor.MoveToIndex(this.currentIndex);

                MerkleTreeNode nodeToLeft = null;
                do
                {
                    if (this.parentCursor.TryMoveLeft())
                        nodeToLeft = this.parentCursor.ReadNode();
                    else
                        nodeToLeft = null;
                } while (nodeToLeft != null && this.deletedIndices.Contains(nodeToLeft.Index));

                this.indicesToLeft[this.currentIndex] = (nodeToLeft != null ? nodeToLeft.Index : (int?)null);
                if (nodeToLeft != null)
                    this.indicesToRight[nodeToLeft.Index] = this.currentIndex;

                if (nodeToLeft != null)
                    this.currentIndex = nodeToLeft.Index;
                else
                    this.beforeStart = true;

                return nodeToLeft != null;
            }
        }

        public bool TryMoveRight()
        {
            if (this.currentIndex == -1)
                throw new InvalidOperationException();
            else if (this.beforeStart)
            {
                Debug.Assert(this.currentIndex == 0);
                this.beforeStart = false;
                return true;
            }

            int? indexToRight;
            if (this.indicesToRight.TryGetValue(this.currentIndex, out indexToRight))
            {
                if (indexToRight != null)
                {
                    this.MoveToIndex(indexToRight.Value);
                    return true;
                }
                else
                {
                    this.afterEnd = true;
                    return false;
                }
            }
            else
            {
                this.parentCursor.MoveToIndex(this.currentIndex);

                MerkleTreeNode nodeToRight = null;
                do
                {
                    if (this.parentCursor.TryMoveRight())
                        nodeToRight = this.parentCursor.ReadNode();
                    else
                        nodeToRight = null;
                } while (nodeToRight != null && this.deletedIndices.Contains(nodeToRight.Index));

                this.indicesToRight[this.currentIndex] = (nodeToRight != null ? nodeToRight.Index : (int?)null);
                if (nodeToRight != null)
                    this.indicesToLeft[nodeToRight.Index] = this.currentIndex;

                if (nodeToRight != null)
                    this.currentIndex = nodeToRight.Index;
                else
                    this.afterEnd = true;

                return nodeToRight != null;
            }
        }

        public MerkleTreeNode ReadNode()
        {
            if (this.currentIndex == -1 || this.beforeStart || this.afterEnd)
                throw new InvalidOperationException();

            if (this.deletedIndices.Contains(this.currentIndex))
                throw new InvalidOperationException();

            MerkleTreeNode node;
            if (!this.cachedNodes.TryGetValue(this.currentIndex, out node))
            {
                this.parentCursor.MoveToIndex(this.currentIndex);
                node = this.parentCursor.ReadNode();
                
                if (node.Index != this.currentIndex)
                    throw new InvalidOperationException();
                this.cachedNodes[this.currentIndex] = node;
            }

            if (node == null)
                throw new InvalidOperationException();

            return node;
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

        public void DeleteNode()
        {
            if (this.currentIndex == -1 || this.beforeStart || this.afterEnd)
                throw new InvalidOperationException();

            int? nodeToLeftIndex;
            if (this.TryMoveLeft())
            {
                nodeToLeftIndex = this.currentIndex;
            }
            else
            {
                nodeToLeftIndex = null;
            }
            this.MoveRight();

            // try to find the next node to the right, so its index is known
            this.TryMoveRight();
            this.MoveLeft();

            int? nodeToRightIndex;
            if (!this.indicesToRight.TryGetValue(this.currentIndex, out nodeToRightIndex))
                throw new InvalidOperationException();

            if (nodeToLeftIndex != null)
                this.indicesToRight[nodeToLeftIndex.Value] = nodeToRightIndex;
            if (nodeToRightIndex != null)
                this.indicesToLeft[nodeToRightIndex.Value] = nodeToLeftIndex;

            this.cachedNodes.Remove(this.currentIndex);
            this.indicesToLeft.Remove(this.currentIndex);
            this.indicesToRight.Remove(this.currentIndex);

            this.updatedIndices.Remove(this.currentIndex);
            this.deletedIndices.Add(this.currentIndex);

            if (nodeToLeftIndex != null)
            {
                this.currentIndex = nodeToLeftIndex.Value;
            }
            else
            {
                Debug.Assert(this.currentIndex == 0);
                this.beforeStart = true;
            }
        }

        public void ApplyChanges()
        {
            foreach (var updatedIndex in this.updatedIndices)
            {
                if (this.parentCursor.TryMoveToIndex(updatedIndex))
                {
                    this.parentCursor.WriteNode(this.cachedNodes[updatedIndex]);
                }
                else
                {
                    //TODO throw
                }
            }

            foreach (var deletedIndex in this.deletedIndices)
            {
                if (this.parentCursor.TryMoveToIndex(deletedIndex))
                {
                    this.parentCursor.DeleteNode();
                }
                else
                {
                    //TODO throw
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
