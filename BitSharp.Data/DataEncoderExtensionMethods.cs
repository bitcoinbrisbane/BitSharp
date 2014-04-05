using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.ExtensionMethods
{
    public static class DataEncoderExtensionMethods
    {
        public static bool ReadBool(this BinaryReader reader)
        {
            return reader.ReadByte() != 0;
        }

        public static UInt16 Read2Bytes(this BinaryReader reader)
        {
            return reader.ReadUInt16();
        }

        public static UInt16 Read2BytesBE(this BinaryReader reader)
        {
            using (var reverse = reader.ReverseRead(2))
                return reverse.Read2Bytes();
        }

        public static UInt32 Read4Bytes(this BinaryReader reader)
        {
            return reader.ReadUInt32();
        }

        public static UInt64 Read8Bytes(this BinaryReader reader)
        {
            return reader.ReadUInt64();
        }

        public static UInt256 Read32Bytes(this BinaryReader reader)
        {
            return new UInt256(reader.ReadBytes(32));
        }

        public static UInt64 ReadVarInt(this BinaryReader reader)
        {
            var value = reader.ReadByte();
            if (value < 0xFD)
                return value;
            else if (value == 0xFD)
                return reader.Read2Bytes();
            else if (value == 0xFE)
                return reader.Read4Bytes();
            else if (value == 0xFF)
                return reader.Read8Bytes();
            else
                throw new Exception();
        }

        public static byte[] ReadVarBytes(this BinaryReader reader)
        {
            var length = reader.ReadVarInt();
            return reader.ReadBytes(length.ToIntChecked());
        }

        public static string ReadVarString(this BinaryReader reader)
        {
            var rawBytes = reader.ReadVarBytes();
            return Encoding.ASCII.GetString(rawBytes);
        }

        public static string ReadFixedString(this BinaryReader reader, int length)
        {
            var encoded = reader.ReadBytes(length);
            // ignore trailing null bytes in a fixed length string
            var encodedTrimmed = encoded.TakeWhile(x => x != 0).ToArray();
            var decoded = Encoding.ASCII.GetString(encodedTrimmed);

            return decoded;
        }

        private static BinaryReader ReverseRead(this BinaryReader reader, int length)
        {
            var bytes = reader.ReadBytes(length);
            Array.Reverse(bytes);

            var stream = new MemoryStream(bytes);
            return new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        }

        public static ImmutableArray<T> ReadList<T>(this BinaryReader reader, Func<T> decode)
        {
            var length = reader.ReadVarInt().ToIntChecked();

            var list = new T[length];
            for (var i = 0; i < length; i++)
            {
                list[i] = decode();
            }

            return list.ToImmutableArray();
        }

        public static void WriteList<T>(this BinaryWriter writer, ImmutableArray<T> list, Action<T> encode)
        {
            writer.WriteVarInt((UInt64)list.Count);

            for (var i = 0; i < list.Count; i++)
            {
                encode(list[i]);
            }
        }
    }
}
