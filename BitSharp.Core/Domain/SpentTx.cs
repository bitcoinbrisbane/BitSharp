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
        //private readonly UInt256 confirmedBlockHash;
        private readonly int blockIndex;
        private readonly int txIndex;
        private readonly int outputCount;

        public SpentTx(/*UInt256 confirmedBlockHash,*/ int blockIndex, int txIndex, int outputCount)
        {
            //this.confirmedBlockHash = confirmedBlockHash;
            this.blockIndex = blockIndex;
            this.txIndex = txIndex;
            this.outputCount = outputCount;
        }

        //public UInt256 ConfirmedBlockHash { get { return this.confirmedBlockHash; } }
        
        public int BlockIndex { get { return this.blockIndex; } }

        public int TxIndex { get { return this.TxIndex; } }

        public int OutputCount { get { return this.outputCount; } }
    }
}
