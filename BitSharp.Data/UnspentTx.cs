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

        private readonly int hashCode;

        public UnspentTx(UInt256 txHash, OutputStates outputStates)
        {
            this.txHash = txHash;
            this.outputStates = outputStates;

            this.hashCode = txHash.GetHashCode() ^ outputStates.GetHashCode();
        }

        public UnspentTx(UInt256 txHash, int length, OutputState state)
        {
            this.txHash = txHash;
            this.outputStates = new OutputStates(length, state);

            this.hashCode = txHash.GetHashCode() ^ outputStates.GetHashCode();
        }

        public UInt256 TxHash { get { return this.txHash; } }

        public OutputStates OutputStates { get { return this.outputStates; } }

        public UnspentTx SetOutputState(int index, OutputState value)
        {
            return new UnspentTx(this.txHash, this.outputStates.Set(index, value));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is UnspentTx))
                return false;

            return (UnspentTx)obj == this;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public static bool operator ==(UnspentTx left, UnspentTx right)
        {
            return object.ReferenceEquals(left, right) || (!object.ReferenceEquals(left, null) && !object.ReferenceEquals(right, null) && left.TxHash == right.TxHash && left.OutputStates == right.OutputStates);
        }

        public static bool operator !=(UnspentTx left, UnspentTx right)
        {
            return !(left == right);
        }
    }
}
