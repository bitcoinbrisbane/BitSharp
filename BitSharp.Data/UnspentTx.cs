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
        private readonly UInt256 confirmedBlockHash;
        private readonly OutputStates outputStates;

        public UnspentTx(UInt256 confirmedBlockHash, OutputStates outputStates)
        {
            this.confirmedBlockHash = confirmedBlockHash;
            this.outputStates = outputStates;
        }

        public UnspentTx(UInt256 confirmedBlockHash, int length, OutputState state)
        {
            this.confirmedBlockHash = confirmedBlockHash;
            this.outputStates = new OutputStates(length, state);
        }

        public UInt256 ConfirmedBlockHash { get { return this.confirmedBlockHash; } }

        public OutputStates OutputStates { get { return this.outputStates; } }

        public UnspentTx SetOutputState(int index, OutputState value)
        {
            return new UnspentTx(this.confirmedBlockHash, this.outputStates.Set(index, value));
        }

        public SpentTx ToSpent()
        {
            return new SpentTx(this.confirmedBlockHash, this.outputStates.Length);
        }
    }
}
