using BitSharp.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public enum OutputState
    {
        Unspent,
        Spent
    }

    public class OutputStates : IEnumerable<OutputState>
    {
        private readonly ImmutableBitArray bitArray;

        public OutputStates(int length, OutputState state)
        {
            this.bitArray = new ImmutableBitArray(length, Encode(state));
        }

        public OutputStates(byte[] bytes, int length)
        {
            this.bitArray = new ImmutableBitArray(bytes, length);
        }

        public OutputStates(ImmutableBitArray bitArray)
        {
            this.bitArray = bitArray;
        }

        public OutputState this[int index]
        {
            get { return Decode(this.bitArray[index]); }
        }

        public int Length
        {
            get { return this.bitArray.Length; }
        }

        public OutputStates Set(int index, OutputState value)
        {
            return new OutputStates(this.bitArray.Set(index, Encode(value)));
        }

        public IEnumerator<OutputState> GetEnumerator()
        {
            for (var i = 0; i < this.bitArray.Length; i++)
                yield return Decode(this.bitArray[i]);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.bitArray.GetEnumerator();
        }

        public byte[] ToByteArray()
        {
            return this.bitArray.ToByteArray();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is OutputStates))
                return false;

            return (OutputStates)obj == this;
        }

        public override int GetHashCode()
        {
            return this.bitArray.GetHashCode();
        }

        public static bool operator ==(OutputStates left, OutputStates right)
        {
            return object.ReferenceEquals(left, right) || (!object.ReferenceEquals(left, null) && !object.ReferenceEquals(right, null) && left.SequenceEqual(right));
        }

        public static bool operator !=(OutputStates left, OutputStates right)
        {
            return !(left == right);
        }

        private bool Encode(OutputState state)
        {
            return state == OutputState.Unspent;
        }

        private OutputState Decode(bool value)
        {
            return value ? OutputState.Unspent : OutputState.Spent;
        }
    }
}
