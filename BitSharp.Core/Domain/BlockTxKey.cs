using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    internal class BlockTxKey
    {
        private readonly UInt256 blockHash;
        private readonly int txIndex;

        public BlockTxKey(UInt256 blockHash, int txIndex)
        {
            this.blockHash = blockHash;
            this.txIndex = txIndex;
        }

        public UInt256 BlockHash { get { return this.blockHash; } }

        public int TxIndex { get { return this.txIndex; } }
    }
}
