using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Network.ExtensionMethods
{
    public static class ReaderExtensionMethods
    {
        public static ImmutableList<T> DecodeList<T>(this BinaryReader reader, Func<T> decode)
        {
            var length = reader.ReadVarInt().ToIntChecked();

            var list = ImmutableList.CreateBuilder<T>();
            for (var i = 0; i < length; i++)
            {
                list.Add(decode());
            }

            return list.ToImmutable();
        }
    }
}
