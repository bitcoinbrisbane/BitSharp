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
        private readonly UInt256 _txHash;
        private readonly ImmutableBitArray _unspentOutputs;

        private readonly int hashCode;

        public UnspentTx(UInt256 txHash, ImmutableBitArray unspentOutputs)
        {
            this._txHash = txHash;
            this._unspentOutputs = unspentOutputs;

            this.hashCode = txHash.GetHashCode() ^ unspentOutputs.GetHashCode();
        }

        public UInt256 TxHash { get { return this._txHash; } }

        public ImmutableBitArray UnspentOutputs { get { return this._unspentOutputs; } }

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
            return object.ReferenceEquals(left, right) || (!object.ReferenceEquals(left, null) && !object.ReferenceEquals(right, null) && left.TxHash == right.TxHash && left.UnspentOutputs == right.UnspentOutputs);
        }

        public static bool operator !=(UnspentTx left, UnspentTx right)
        {
            return !(left == right);
        }
    }
}
