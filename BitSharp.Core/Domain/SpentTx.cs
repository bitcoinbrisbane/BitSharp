using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class SpentTx
    {
        private readonly int blockIndex;
        private readonly int txIndex;
        private readonly int outputCount;

        public SpentTx(int blockIndex, int txIndex, int outputCount)
        {
            this.blockIndex = blockIndex;
            this.txIndex = txIndex;
            this.outputCount = outputCount;
        }

        public int BlockIndex { get { return this.blockIndex; } }

        public int TxIndex { get { return this.TxIndex; } }

        public int OutputCount { get { return this.outputCount; } }
    }
}
