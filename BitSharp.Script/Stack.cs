using BitSharp.Common;
using BitSharp.Script;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Script
{
    public class Stack
    {
        private Stack<ImmutableList<byte>> stack = new Stack<ImmutableList<byte>>();

        public int Count { get { return stack.Count; } }

        // Peek
        public ImmutableList<byte> PeekBytes()
        {
            return stack.Peek();
        }

        public bool PeekBool()
        {
            return CastToBool(stack.Peek());
        }

        public BigInteger PeekBigInteger()
        {
            return CastToBigInteger(stack.Peek());
        }
        
        // Pop
        public ImmutableList<byte> PopBytes()
        {
            return stack.Pop();
        }

        public bool PopBool()
        {
            return CastToBool(stack.Pop());
        }

        public BigInteger PopBigInteger()
        {
            return CastToBigInteger(stack.Pop());
        }

        // Push
        public void PushBytes(byte[] value)
        {
            stack.Push(value.ToImmutableList());
        }

        public void PushBytes(ImmutableList<byte> value)
        {
            stack.Push(value);
        }

        public void PushBool(bool value)
        {
            if (value)
                stack.Push(ImmutableList.Create((byte)1));
            else
                stack.Push(ImmutableList.Create<byte>());
        }

        public void PushBigInteger(BigInteger value)
        {
            stack.Push(value.ToByteArray().ToImmutableList());
        }

        private bool CastToBool(ImmutableList<byte> value)
        {
            for (var i = 0; i < value.Count; i++)
            {
                if (value[i] != 0)
                {
                    // Can be negative zero
                    if (i == value.Count - 1 && value[i] == 0x80)
                        return false;
                    
                    return true;
                }
            }
            
            return false;
        }

        private BigInteger CastToBigInteger(ImmutableList<byte> value)
        {
            return new BigInteger(value.ToArray());
        }
    }
}
