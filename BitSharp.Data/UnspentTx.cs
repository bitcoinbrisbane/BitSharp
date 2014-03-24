using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class UnspentTx
    {
        private readonly UInt256 txHash;
        private readonly OutputStates outputStates;

        public UnspentTx(UInt256 txHash, OutputStates outputStates)
        {
            this.txHash = txHash;
            this.outputStates = outputStates;
        }

        public UnspentTx(UInt256 txHash, int length, OutputState state)
        {
            this.txHash = txHash;
            this.outputStates = new OutputStates(length, state);
        }

        public UInt256 TxHash { get { return this.txHash; } }

        public OutputStates OutputStates { get { return this.outputStates; } }

        public UnspentTx SetOutputState(int index, OutputState value)
        {
            return new UnspentTx(this.txHash, this.outputStates.Set(index, value));
        }
    }
}
