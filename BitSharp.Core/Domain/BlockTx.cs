using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class BlockTx : BlockElement
    {
        private readonly ImmutableArray<byte> txBytes;

        public BlockTx(UInt256 blockHash, int index, int depth, UInt256 hash, ImmutableArray<byte> txBytes)
            : base(blockHash, index, depth, hash)
        {
            this.txBytes = txBytes;
        }

        public ImmutableArray<byte> TxBytes { get { return this.txBytes; } }
    }
}
