using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class BlockElement
    {
        private readonly UInt256 blockHash;
        private readonly int index;
        private readonly int depth;
        private readonly UInt256 hash;

        public BlockElement(UInt256 blockHash, int index, int depth, UInt256 hash)
        {
            this.blockHash = blockHash;
            this.index = index;
            this.depth = depth;
            this.hash = hash;
        }

        public UInt256 BlockHash { get { return this.blockHash; } }
        
        public int Index { get { return this.index; } }
        
        public int Depth { get { return this.depth; } }
        
        public UInt256 Hash { get { return this.hash; } }

        public BlockTx ToBlockTx(ImmutableArray<byte> txBytes)
        {
            return new BlockTx(this.blockHash, this.index, this.depth, this.hash, txBytes);
        }
    }
}
