using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common.ExtensionMethods
{
    public static class WriterExtensionMethods
    {
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
    }
}
