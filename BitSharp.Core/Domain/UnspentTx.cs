using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class UnspentTx
    {
        private readonly UInt256 confirmedBlockHash;
        private readonly int txIndex;
        private readonly OutputStates outputStates;

        public UnspentTx(UInt256 confirmedBlockHash, int txIndex, OutputStates outputStates)
        {
            this.confirmedBlockHash = confirmedBlockHash;
            this.txIndex = txIndex;
            this.outputStates = outputStates;
        }

        public UnspentTx(UInt256 confirmedBlockHash, int txIndex, int length, OutputState state)
        {
            this.confirmedBlockHash = confirmedBlockHash;
            this.txIndex = txIndex;
            this.outputStates = new OutputStates(length, state);
        }

        public UInt256 ConfirmedBlockHash { get { return this.confirmedBlockHash; } }

        public int TxIndex { get { return this.txIndex; } }

        public OutputStates OutputStates { get { return this.outputStates; } }

        public UnspentTx SetOutputState(int index, OutputState value)
        {
            return new UnspentTx(this.confirmedBlockHash, this.txIndex, this.outputStates.Set(index, value));
        }

        public SpentTx ToSpent()
        {
            return new SpentTx(this.confirmedBlockHash, this.txIndex, this.outputStates.Length);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is UnspentTx))
                return false;

            var other = (UnspentTx)obj;
            return other.confirmedBlockHash == this.confirmedBlockHash && other.outputStates.Equals(this.outputStates);
        }

        public override int GetHashCode()
        {
            return this.confirmedBlockHash.GetHashCode() ^ this.outputStates.GetHashCode();
        }
    }
}
