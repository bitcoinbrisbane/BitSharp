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
        private readonly int blockIndex;
        private readonly int txIndex;
        private readonly OutputStates outputStates;

        public UnspentTx(int blockIndex, int txIndex, OutputStates outputStates)
        {
            this.blockIndex = blockIndex;
            this.txIndex = txIndex;
            this.outputStates = outputStates;
        }

        public UnspentTx(int blockIndex, int txIndex, int length, OutputState state)
        {
            this.blockIndex = blockIndex;
            this.txIndex = txIndex;
            this.outputStates = new OutputStates(length, state);
        }

        public int BlockIndex { get { return this.blockIndex; } }

        public int TxIndex { get { return this.txIndex; } }

        public OutputStates OutputStates { get { return this.outputStates; } }

        public UnspentTx SetOutputState(int index, OutputState value)
        {
            return new UnspentTx(this.blockIndex, this.txIndex, this.outputStates.Set(index, value));
        }

        public SpentTx ToSpent()
        {
            return new SpentTx(this.blockIndex, this.txIndex, this.outputStates.Length);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is UnspentTx))
                return false;

            var other = (UnspentTx)obj;
            return other.blockIndex == this.blockIndex && other.txIndex == this.txIndex && other.outputStates.Equals(this.outputStates);
        }

        public override int GetHashCode()
        {
            return this.blockIndex.GetHashCode() ^ this.txIndex.GetHashCode() ^ this.outputStates.GetHashCode();
        }
    }
}
