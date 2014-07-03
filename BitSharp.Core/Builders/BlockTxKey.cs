using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class BlockTxKey
    {
        public readonly UInt256 blockHash;
        public readonly int txIndex;

        public BlockTxKey(UInt256 blockHash, int txIndex)
        {
            this.blockHash = blockHash;
            this.txIndex = txIndex;
        }
    }
}
