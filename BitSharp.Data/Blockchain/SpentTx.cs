using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class SpentTx
    {
        private readonly UInt256 confirmedBlockHash;
        private readonly int outputCount;

        public SpentTx(UInt256 confirmedBlockHash, int outputCount)
        {
            this.confirmedBlockHash = confirmedBlockHash;
            this.outputCount = outputCount;
        }

        public UInt256 ConfirmedBlockHash { get { return this.confirmedBlockHash; } }

        public int OutputCount { get { return this.outputCount; } }
    }
}
