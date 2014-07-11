using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryMerkleTreePruningCursor : IMerkleTreePruningCursor
    {
        private readonly List<BlockTx> nodes;
        private int index;

        public MemoryMerkleTreePruningCursor(IEnumerable<MerkleTreeNode> nodes)
        {
            this.nodes = new List<BlockTx>(nodes.Select(x => new BlockTx(x.Index, x.Depth, x.Hash, x.Pruned, null)));
            this.index = -1;
        }

        public MemoryMerkleTreePruningCursor(IEnumerable<BlockTx> nodes)
        {
            this.nodes = new List<BlockTx>(nodes);
            this.index = -1;
        }

        public void Dispose()
        {
        }

        public void BeginTransaction()
        {
        }

        public void CommitTransaction()
        {
        }

        public bool TryMoveToIndex(int index, out MerkleTreeNode node)
        {
            this.index = this.nodes.FindIndex(x => x.Index == index);

            if (this.index >= 0 && this.index < this.nodes.Count)
            {
                node = this.nodes[this.index];
                return true;
            }
            else
            {
                node = default(MerkleTreeNode);
                return false;
            }
        }

        public bool TryMoveLeft(out MerkleTreeNode node)
        {
            if (this.index >= 0 && this.index < this.nodes.Count)
            {
                var newIndex = this.index - 1;
                if (newIndex >= 0 && newIndex < this.nodes.Count)
                {
                    this.index = newIndex;
                    node = this.nodes[newIndex];
                    return true;
                }
            }

            node = default(MerkleTreeNode);
            return false;
        }

        public bool TryMoveRight(out MerkleTreeNode node)
        {
            if (this.index >= 0 && this.index < this.nodes.Count)
            {
                var newIndex = this.index + 1;
                if (newIndex >= 0 && newIndex < this.nodes.Count)
                {
                    this.index = newIndex;
                    node = this.nodes[newIndex];
                    return true;
                }
            }

            node = default(MerkleTreeNode);
            return false;
        }

        public void WriteNode(MerkleTreeNode node)
        {
            if (!node.Pruned)
                throw new InvalidOperationException();
            if (this.index < 0 || this.index >= this.nodes.Count)
                throw new InvalidOperationException();

            this.nodes[this.index] = new BlockTx(node.Index, node.Depth, node.Hash, node.Pruned, null);
        }

        public void MoveLeft()
        {
            MerkleTreeNode node;
            if (!this.TryMoveLeft(out node))
                throw new InvalidOperationException();
        }

        public void DeleteNodeToRight()
        {
            if (this.index < 0 || this.index >= this.nodes.Count)
                throw new InvalidOperationException();

            var removeIndex = this.index + 1;
            if (removeIndex < 0 || removeIndex >= this.nodes.Count)
                throw new InvalidOperationException();

            this.nodes.RemoveAt(removeIndex);
        }

        public IEnumerable<BlockTx> ReadNodes()
        {
            return this.nodes;
        }
    }
}
