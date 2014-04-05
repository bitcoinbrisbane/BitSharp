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
        #region Reader Methods
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
        #endregion

        #region Writer Methods
        public static void WriteUInt32LE(this BinaryWriter writer, UInt32 value)
        {
            writer.Write(value);
        }

        public static void WriteBool(this BinaryWriter writer, bool value)
        {
            writer.Write((byte)(value ? 1 : 0));
        }

        public static void Write1Byte(this BinaryWriter writer, Byte value)
        {
            writer.Write(value);
        }

        public static void Write2Bytes(this BinaryWriter writer, UInt16 value)
        {
            writer.Write(value);
        }

        public static void Write2BytesBE(this BinaryWriter writer, UInt16 value)
        {
            writer.ReverseWrite(2, reverseWriter => reverseWriter.Write2Bytes(value));
        }

        public static void Write4Bytes(this BinaryWriter writer, UInt32 value)
        {
            writer.Write(value);
        }

        public static void WriteInt32(this BinaryWriter writer, Int32 value)
        {
            writer.Write(value);
        }

        public static void Write8Bytes(this BinaryWriter writer, UInt64 value)
        {
            writer.Write(value);
        }

        public static void Write32Bytes(this BinaryWriter writer, UInt256 value)
        {
            writer.Write(value.ToByteArray());
        }

        public static void WriteBytes(this BinaryWriter writer, byte[] value)
        {
            writer.Write(value);
        }

        public static void WriteBytes(this BinaryWriter writer, int length, byte[] value)
        {
            if (value.Length != length)
                throw new ArgumentException();

            writer.WriteBytes(value);
        }

        public static void WriteVarInt(this BinaryWriter writer, UInt64 value)
        {
            if (value < 0xFD)
            {
                writer.Write1Byte((Byte)value);
            }
            else if (value <= 0xFFFF)
            {
                writer.Write1Byte(0xFD);
                writer.Write2Bytes((UInt16)value);
            }
            else if (value <= 0xFFFFFFFF)
            {
                writer.Write1Byte(0xFE);
                writer.Write4Bytes((UInt32)value);
            }
            else
            {
                writer.Write1Byte(0xFF);
                writer.Write8Bytes(value);
            }
        }

        public static void WriteVarBytes(this BinaryWriter writer, byte[] value)
        {
            writer.WriteVarInt((UInt64)value.Length);
            writer.WriteBytes(value.Length, value);
        }

        public static void WriteVarString(this BinaryWriter writer, string value)
        {
            var encoded = Encoding.ASCII.GetBytes(value);
            writer.WriteVarBytes(encoded);
        }

        public static void WriteFixedString(this BinaryWriter writer, int length, string value)
        {
            if (value.Length < length)
                value = value.PadRight(length, '\0');
            if (value.Length != length)
                throw new ArgumentException();

            var encoded = Encoding.ASCII.GetBytes(value);
            writer.WriteBytes(encoded.Length, encoded);
        }

        private static void ReverseWrite(this BinaryWriter writer, int length, Action<BinaryWriter> write)
        {
            var bytes = new byte[length];
            using (var stream = new MemoryStream(bytes))
            using (var reverseWriter = new BinaryWriter(stream))
            {
                write(reverseWriter);

                // verify that the correct amount of bytes were writtern
                if (reverseWriter.BaseStream.Position != length)
                    throw new InvalidOperationException();
            }
            Array.Reverse(bytes);

            writer.WriteBytes(bytes);
        }

        public static void WriteList<T>(this BinaryWriter writer, ImmutableArray<T> list, Action<T> encode)
        {
            writer.WriteVarInt((UInt64)list.Count);

            for (var i = 0; i < list.Count; i++)
            {
                encode(list[i]);
            }
        }
        #endregion
    }
}
