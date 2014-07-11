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
        private readonly bool pruned;

        public BlockElement(int index, int depth, UInt256 hash, bool pruned)
            : base(index, depth, hash)
        {
            this.pruned = pruned;
        }

        public BlockElement(MerkleTreeNode node, bool pruned)
            : base(node.Index, node.Depth, node.Hash)
        {
            this.pruned = pruned;
        }

        public bool Pruned { get { return this.pruned; } }

        public BlockTx ToBlockTx(Transaction transaction)
        {
            if (this.Depth != 0 || this.Pruned)
                throw new InvalidOperationException();

            return new BlockTx(this.Index, this.Depth, this.Hash, /*pruned:*/transaction == null, transaction);
        }

        public BlockElement AsPruned()
        {
            if (this.Depth != 0)
                throw new InvalidOperationException();

            return new BlockElement(this.Index, this.Depth, this.Hash, pruned: true);
        }

        public BlockElement PairWith(BlockElement right)
        {
            if (!this.pruned || !right.pruned)
                throw new InvalidOperationException();

            return new BlockElement(Pair(this, right), pruned: true);
        }

        public new BlockElement PairWithSelf()
        {
            if (!this.pruned)
                throw new InvalidOperationException();

            return new BlockElement(PairWithSelf(this), pruned: true);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BlockElement))
                return false;

            var other = (BlockElement)obj;
            return base.Equals(obj) && other.pruned == this.pruned;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() ^ this.pruned.GetHashCode();
        }

        public static bool operator ==(BlockElement left, BlockElement right)
        {
            return object.ReferenceEquals(left, right) || (!object.ReferenceEquals(left, null) && !object.ReferenceEquals(right, null) && left.Equals(right));
        }

        public static bool operator !=(BlockElement left, BlockElement right)
        {
            return !(left == right);
        }
    }
}
