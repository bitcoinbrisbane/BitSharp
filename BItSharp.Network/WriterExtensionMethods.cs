using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Network.ExtensionMethods
{
    public static class WriterExtensionMethods
    {
        public static void EncodeList<T>(this BinaryWriter writer, ImmutableArray<T> list, Action<T> encode)
        {
            writer.WriteVarInt((UInt64)list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                encode(list[i]);
            }
        }
    }
}
