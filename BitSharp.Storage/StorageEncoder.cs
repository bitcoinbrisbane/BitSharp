using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class StorageEncoder
    {
        public static UInt256 DecodeUInt256(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return reader.Read32Bytes();
            }
        }

        public static UInt256 DecodeUInt256(UInt256 confirmedBlockHash, byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeUInt256(stream);
            }
        }

        public static void EncodeUInt256(Stream stream, UInt256 value)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write32Bytes(value);
            }
        }

        public static byte[] EncodeUInt256(UInt256 value)
        {
            using (var stream = new MemoryStream())
            {
                EncodeUInt256(stream, value);
                return stream.ToArray();
            }
        }

        public static Block DecodeBlock(Stream stream, UInt256? blockHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new Block
                (
                    header: DecodeBlockHeader(stream, blockHash),
                    transactions: DecodeList(reader, () => DecodeTransaction(stream))
                );
            }
        }

        public static Block DecodeBlock(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeBlock(stream);
            }
        }

        public static void EncodeBlock(Stream stream, Block block)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                EncodeBlockHeader(stream, block.Header);
                EncodeList(writer, block.Transactions, tx => EncodeTransaction(stream, tx));
            }
        }

        public static byte[] EncodeBlock(Block block)
        {
            using (var stream = new MemoryStream())
            {
                EncodeBlock(stream, block);
                return stream.ToArray();
            }
        }

        public static BlockHeader DecodeBlockHeader(Stream stream, UInt256? blockHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new BlockHeader
                (
                    version: reader.Read4Bytes(),
                    previousBlock: reader.Read32Bytes(),
                    merkleRoot: reader.Read32Bytes(),
                    time: reader.Read4Bytes(),
                    bits: reader.Read4Bytes(),
                    nonce: reader.Read4Bytes(),
                    hash: blockHash
                );
            }
        }

        public static BlockHeader DecodeBlockHeader(byte[] bytes, UInt256? blockHash = null)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeBlockHeader(stream, blockHash);
            }
        }

        public static void EncodeBlockHeader(Stream stream, BlockHeader blockHeader)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(blockHeader.Version);
                writer.Write32Bytes(blockHeader.PreviousBlock);
                writer.Write32Bytes(blockHeader.MerkleRoot);
                writer.Write4Bytes(blockHeader.Time);
                writer.Write4Bytes(blockHeader.Bits);
                writer.Write4Bytes(blockHeader.Nonce);
            }
        }

        public static byte[] EncodeBlockHeader(BlockHeader blockHeader)
        {
            using (var stream = new MemoryStream())
            {
                EncodeBlockHeader(stream, blockHeader);
                return stream.ToArray();
            }
        }

        public static BigInteger DecodeTotalWork(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                var totalWorkBytes = reader.ReadBytes(64);
                return new BigInteger(totalWorkBytes);
            }
        }

        public static BigInteger DecodeTotalWork(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeTotalWork(stream);
            }
        }

        public static void EncodeTotalWork(Stream stream, BigInteger totalWork)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                var totalWorkBytes = totalWork.ToByteArray();
                if (totalWorkBytes.Length > 64)
                    throw new ArgumentOutOfRangeException();

                var totalWorkBytes64 = new byte[64];
                Buffer.BlockCopy(totalWorkBytes, 0, totalWorkBytes64, 0, totalWorkBytes.Length);

                writer.WriteBytes(totalWorkBytes64);
                Debug.Assert(new BigInteger(totalWorkBytes64) == totalWork);
            }
        }

        public static byte[] EncodeTotalWork(BigInteger totalWork)
        {
            using (var stream = new MemoryStream())
            {
                EncodeTotalWork(stream, totalWork);
                return stream.ToArray();
            }
        }

        public static ChainedBlock DecodeChainedBlock(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new ChainedBlock
                (
                    blockHash: reader.Read32Bytes(),
                    previousBlockHash: reader.Read32Bytes(),
                    height: reader.ReadInt32(),
                    totalWork: new BigInteger(reader.ReadVarBytes())
                );
            }
        }

        public static ChainedBlock DecodeChainedBlock(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeChainedBlock(stream);
            }
        }

        public static void EncodeChainedBlock(Stream stream, ChainedBlock chainedBlock)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write32Bytes(chainedBlock.BlockHash);
                writer.Write32Bytes(chainedBlock.PreviousBlockHash);
                writer.WriteInt32(chainedBlock.Height);
                writer.WriteVarBytes(chainedBlock.TotalWork.ToByteArray());
            }
        }

        public static byte[] EncodeChainedBlock(ChainedBlock chainedBlock)
        {
            using (var stream = new MemoryStream())
            {
                EncodeChainedBlock(stream, chainedBlock);
                return stream.ToArray();
            }
        }

        public static Transaction DecodeTransaction(Stream stream, UInt256? txHash = null)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new Transaction
                (
                    version: reader.Read4Bytes(),
                    inputs: DecodeList(reader, () => DecodeTxInput(stream)),
                    outputs: DecodeList(reader, () => DecodeTxOutput(stream)),
                    lockTime: reader.Read4Bytes(),
                    hash: txHash
                );
            }
        }

        public static Transaction DecodeTransaction(byte[] bytes, UInt256? txHash = null)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeTransaction(stream, txHash);
            }
        }

        public static void EncodeTransaction(Stream stream, Transaction tx)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write4Bytes(tx.Version);
                EncodeList(writer, tx.Inputs, input => EncodeTxInput(stream, input));
                EncodeList(writer, tx.Outputs, output => EncodeTxOutput(stream, output));
                writer.Write4Bytes(tx.LockTime);
            }
        }

        public static byte[] EncodeTransaction(Transaction tx)
        {
            using (var stream = new MemoryStream())
            {
                EncodeTransaction(stream, tx);
                return stream.ToArray();
            }
        }

        public static TxInput DecodeTxInput(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new TxInput
                (
                    previousTxOutputKey: new TxOutputKey
                    (
                        txHash: reader.Read32Bytes(),
                        txOutputIndex: reader.Read4Bytes()
                    ),
                    scriptSignature: DecodeVarBytes(reader).ToImmutableArray(),
                    sequence: reader.Read4Bytes()
                );
            }
        }

        public static TxInput DecodeTxInput(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeTxInput(stream);
            }
        }

        public static void EncodeTxInput(Stream stream, TxInput txInput)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write32Bytes(txInput.PreviousTxOutputKey.TxHash);
                writer.Write4Bytes(txInput.PreviousTxOutputKey.TxOutputIndex);
                EncodeVarBytes(writer, txInput.ScriptSignature.ToArray());
                writer.Write4Bytes(txInput.Sequence);
            }
        }

        public static byte[] EncodeTxInput(TxInput txInput)
        {
            using (var stream = new MemoryStream())
            {
                EncodeTxInput(stream, txInput);
                return stream.ToArray();
            }
        }

        public static TxOutput DecodeTxOutput(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new TxOutput
                (
                    value: reader.Read8Bytes(),
                    scriptPublicKey: DecodeVarBytes(reader).ToImmutableArray()
                );
            }
        }

        public static TxOutput DecodeTxOutput(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeTxOutput(stream);
            }
        }

        public static void EncodeTxOutput(Stream stream, TxOutput txOutput)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write8Bytes(txOutput.Value);
                EncodeVarBytes(writer, txOutput.ScriptPublicKey.ToArray());
            }
        }

        public static byte[] EncodeTxOutput(TxOutput txOutput)
        {
            using (var stream = new MemoryStream())
            {
                EncodeTxOutput(stream, txOutput);
                return stream.ToArray();
            }
        }

        public static UnspentTx DecodeUnspentTx(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new UnspentTx(
                    confirmedBlockHash: reader.Read32Bytes(),
                    outputStates: new OutputStates(
                        bytes: reader.ReadVarBytes(),
                        length: reader.ReadInt32())
                );
            }
        }

        public static UnspentTx DecodeUnspentTx(UInt256 confirmedBlockHash, byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeUnspentTx(stream);
            }
        }

        public static void EncodeUnspentTx(Stream stream, UnspentTx unspentTx)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write32Bytes(unspentTx.ConfirmedBlockHash);
                writer.WriteVarBytes(unspentTx.OutputStates.ToByteArray());
                writer.WriteInt32(unspentTx.OutputStates.Length);
            }
        }

        public static byte[] EncodeUnspentTx(UnspentTx unspentTx)
        {
            using (var stream = new MemoryStream())
            {
                EncodeUnspentTx(stream, unspentTx);
                return stream.ToArray();
            }
        }

        public static SpentTx DecodeSpentTx(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new SpentTx(
                    confirmedBlockHash: reader.Read32Bytes(),
                    outputCount: reader.ReadInt32()
                );
            }
        }

        public static SpentTx DecodeSpentTx(UInt256 confirmedBlockHash, byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeSpentTx(stream);
            }
        }

        public static void EncodeSpentTx(Stream stream, SpentTx spentTx)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write32Bytes(spentTx.ConfirmedBlockHash);
                writer.WriteInt32(spentTx.OutputCount);
            }
        }

        public static byte[] EncodeSpentTx(SpentTx spentTx)
        {
            using (var stream = new MemoryStream())
            {
                EncodeSpentTx(stream, spentTx);
                return stream.ToArray();
            }
        }

        public static OutputStates DecodeOutputStates(UInt256 txHash, Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new OutputStates
                (
                    bytes: reader.ReadVarBytes(),
                    length: reader.ReadInt32()
                );
            }
        }

        public static OutputStates DecodeOutputStates(UInt256 txHash, byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeOutputStates(txHash, stream);
            }
        }

        public static void EncodeOutputStates(Stream stream, OutputStates outputStates)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteVarBytes(outputStates.ToByteArray());
                writer.WriteInt32(outputStates.Length);
            }
        }

        public static byte[] EncodeOutputStates(OutputStates outputStates)
        {
            using (var stream = new MemoryStream())
            {
                EncodeOutputStates(stream, outputStates);
                return stream.ToArray();
            }
        }

        public static TxOutputKey DecodeTxOutputKey(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new TxOutputKey
                (
                    txHash: reader.Read32Bytes(),
                    txOutputIndex: reader.ReadUInt32()
                );
            }
        }

        public static TxOutputKey DecodeTxOutputKey(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeTxOutputKey(stream);
            }
        }

        public static void EncodeTxOutputKey(Stream stream, TxOutputKey txOutputKey)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write32Bytes(txOutputKey.TxHash);
                writer.Write4Bytes(txOutputKey.TxOutputIndex);
            }
        }

        public static byte[] EncodeTxOutputKey(TxOutputKey txOutputKey)
        {
            using (var stream = new MemoryStream())
            {
                EncodeTxOutputKey(stream, txOutputKey);
                return stream.ToArray();
            }
        }

        public static byte[] DecodeVarBytes(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            return reader.ReadBytes(length);
        }

        public static void EncodeVarBytes(BinaryWriter writer, byte[] bytes)
        {
            writer.WriteInt32(bytes.Length);
            writer.WriteBytes(bytes);
        }

        public static string DecodeVarString(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                var length = reader.ReadInt32();
                return Encoding.ASCII.GetString(reader.ReadBytes(length));
            }
        }

        public static string DecodeVarString(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeVarString(stream);
            }
        }

        public static void EncodeVarString(Stream stream, string s)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                var bytes = Encoding.ASCII.GetBytes(s);
                writer.WriteInt32(bytes.Length);
                writer.WriteBytes(bytes);
            }
        }

        public static byte[] EncodeVarString(string s)
        {
            using (var stream = new MemoryStream())
            {
                EncodeVarString(stream, s);
                return stream.ToArray();
            }
        }

        public static ImmutableArray<T> DecodeList<T>(BinaryReader reader, Func<T> decode)
        {
            var length = reader.ReadInt32();

            var list = new T[length];
            for (var i = 0; i < length; i++)
            {
                list[i] = decode();
            }

            return list.ToImmutableArray();
        }

        public static void EncodeList<T>(BinaryWriter writer, ImmutableArray<T> list, Action<T> encode)
        {
            writer.WriteInt32(list.Count);

            for (var i = 0; i < list.Count; i++)
            {
                encode(list[i]);
            }
        }
    }
}
