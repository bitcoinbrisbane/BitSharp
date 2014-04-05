using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class DataEncoder
    {
        public static UInt256 DecodeUInt256(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return reader.ReadUInt256();
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
                writer.WriteUInt256(value);
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
                    transactions: reader.ReadList(() => DecodeTransaction(stream))
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
                writer.WriteList(block.Transactions, tx => EncodeTransaction(stream, tx));
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
                    version: reader.ReadUInt32(),
                    previousBlock: reader.ReadUInt256(),
                    merkleRoot: reader.ReadUInt256(),
                    time: reader.ReadUInt32(),
                    bits: reader.ReadUInt32(),
                    nonce: reader.ReadUInt32(),
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
                writer.WriteUInt32(blockHeader.Version);
                writer.WriteUInt256(blockHeader.PreviousBlock);
                writer.WriteUInt256(blockHeader.MerkleRoot);
                writer.WriteUInt32(blockHeader.Time);
                writer.WriteUInt32(blockHeader.Bits);
                writer.WriteUInt32(blockHeader.Nonce);
            }
        }

        public static byte[] EncodeBlockHeader(UInt32 Version, UInt256 PreviousBlock, UInt256 MerkleRoot, UInt32 Time, UInt32 Bits, UInt32 Nonce)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteUInt32(Version);
                writer.WriteUInt256(PreviousBlock);
                writer.WriteUInt256(MerkleRoot);
                writer.WriteUInt32(Time);
                writer.WriteUInt32(Bits);
                writer.WriteUInt32(Nonce);

                return stream.ToArray();
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
                    blockHash: reader.ReadUInt256(),
                    previousBlockHash: reader.ReadUInt256(),
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
                writer.WriteUInt256(chainedBlock.BlockHash);
                writer.WriteUInt256(chainedBlock.PreviousBlockHash);
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
                    version: reader.ReadUInt32(),
                    inputs: reader.ReadList(() => DecodeTxInput(stream)),
                    outputs: reader.ReadList(() => DecodeTxOutput(stream)),
                    lockTime: reader.ReadUInt32(),
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
                writer.WriteUInt32(tx.Version);
                writer.WriteList(tx.Inputs, input => EncodeTxInput(stream, input));
                writer.WriteList(tx.Outputs, output => EncodeTxOutput(stream, output));
                writer.WriteUInt32(tx.LockTime);
            }
        }

        public static byte[] EncodeTransaction(UInt32 Version, ImmutableArray<TxInput> Inputs, ImmutableArray<TxOutput> Outputs, UInt32 LockTime)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteUInt32(Version);
                writer.WriteList(Inputs, input => EncodeTxInput(stream, input));
                writer.WriteList(Outputs, output => EncodeTxOutput(stream, output));
                writer.WriteUInt32(LockTime);

                return stream.ToArray();
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
                        txHash: reader.ReadUInt256(),
                        txOutputIndex: reader.ReadUInt32()
                    ),
                    scriptSignature: reader.ReadVarBytes().ToImmutableArray(),
                    sequence: reader.ReadUInt32()
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
                writer.WriteUInt256(txInput.PreviousTxOutputKey.TxHash);
                writer.WriteUInt32(txInput.PreviousTxOutputKey.TxOutputIndex);
                writer.WriteVarBytes(txInput.ScriptSignature.ToArray());
                writer.WriteUInt32(txInput.Sequence);
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
                    value: reader.ReadUInt64(),
                    scriptPublicKey: reader.ReadVarBytes().ToImmutableArray()
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
                writer.WriteUInt64(txOutput.Value);
                writer.WriteVarBytes(txOutput.ScriptPublicKey.ToArray());
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
                    confirmedBlockHash: reader.ReadUInt256(),
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
                writer.WriteUInt256(unspentTx.ConfirmedBlockHash);
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
                    confirmedBlockHash: reader.ReadUInt256(),
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
                writer.WriteUInt256(spentTx.ConfirmedBlockHash);
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
                    txHash: reader.ReadUInt256(),
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
                writer.WriteUInt256(txOutputKey.TxHash);
                writer.WriteUInt32(txOutputKey.TxOutputIndex);
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

        public static AlertPayload DecodeAlertPayload(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new AlertPayload
                (
                    Payload: reader.ReadVarString(),
                    Signature: reader.ReadVarString()
                );
            }
        }

        public static AlertPayload DecodeAlertPayload(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeAlertPayload(stream);
            }
        }

        public static void EncodeAlertPayload(Stream stream, AlertPayload alertPayload)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteVarString(alertPayload.Payload);
                writer.WriteVarString(alertPayload.Signature);
            }
        }

        public static byte[] EncodeAlertPayload(AlertPayload alertPayload)
        {
            using (var stream = new MemoryStream())
            {
                EncodeAlertPayload(stream, alertPayload);
                return stream.ToArray();
            }
        }

        public static AddressPayload DecodeAddressPayload(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new AddressPayload
                (
                    NetworkAddresses: reader.ReadList(() => DecodeNetworkAddressWithTime(stream))
                );
            }
        }

        public static AddressPayload DecodeAddressPayload(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeAddressPayload(stream);
            }
        }

        public static void EncodeAddressPayload(Stream stream, AddressPayload addressPayload)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteList(addressPayload.NetworkAddresses, networkAddress => EncodeNetworkAddressWithTime(stream, networkAddress));
            }
        }

        public static byte[] EncodeAddressPayload(AddressPayload addressPayload)
        {
            using (var stream = new MemoryStream())
            {
                EncodeAddressPayload(stream, addressPayload);
                return stream.ToArray();
            }
        }

        public static GetBlocksPayload DecodeGetBlocksPayload(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new GetBlocksPayload
                (
                    Version: reader.ReadUInt32(),
                    BlockLocatorHashes: reader.ReadList(() => reader.ReadUInt256()),
                    HashStop: reader.ReadUInt256()
                );
            }
        }

        public static GetBlocksPayload DecodeGetBlocksPayload(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeGetBlocksPayload(stream);
            }
        }

        public static void EncodeGetBlocksPayload(Stream stream, GetBlocksPayload getBlocksPayload)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteUInt32(getBlocksPayload.Version);
                writer.WriteList(getBlocksPayload.BlockLocatorHashes, locatorHash => writer.WriteUInt256(locatorHash));
                writer.WriteUInt256(getBlocksPayload.HashStop);
            }
        }

        public static byte[] EncodeGetBlocksPayload(GetBlocksPayload getBlocksPayload)
        {
            using (var stream = new MemoryStream())
            {
                EncodeGetBlocksPayload(stream, getBlocksPayload);
                return stream.ToArray();
            }
        }

        public static InventoryPayload DecodeInventoryPayload(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new InventoryPayload
                (
                    InventoryVectors: reader.ReadList(() => DecodeInventoryVector(stream))
                );
            }
        }

        public static InventoryPayload DecodeInventoryPayload(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeInventoryPayload(stream);
            }
        }

        public static void EncodeInventoryPayload(Stream stream, InventoryPayload invPayload)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteList(invPayload.InventoryVectors, invVector => EncodeInventoryVector(stream, invVector));
            }
        }

        public static byte[] EncodeInventoryPayload(InventoryPayload invPayload)
        {
            using (var stream = new MemoryStream())
            {
                EncodeInventoryPayload(stream, invPayload);
                return stream.ToArray();
            }
        }

        public static InventoryVector DecodeInventoryVector(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new InventoryVector
                (
                    Type: reader.ReadUInt32(),
                    Hash: reader.ReadUInt256()
                );
            }
        }

        public static InventoryVector DecodeInventoryVector(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeInventoryVector(stream);
            }
        }

        public static void EncodeInventoryVector(Stream stream, InventoryVector invVector)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteUInt32(invVector.Type);
                writer.WriteUInt256(invVector.Hash);
            }
        }

        public static byte[] EncodeInventoryVector(InventoryVector invVector)
        {
            using (var stream = new MemoryStream())
            {
                EncodeInventoryVector(stream, invVector);
                return stream.ToArray();
            }
        }

        public static Message DecodeMessage(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                var magic = reader.ReadUInt32();
                var command = reader.ReadFixedString(12);
                var payloadSize = reader.ReadUInt32();
                var payloadChecksum = reader.ReadUInt32();
                var payload = reader.ReadBytes(payloadSize.ToIntChecked()).ToImmutableArray();

                return new Message
                (
                    Magic: magic,
                    Command: command,
                    PayloadSize: payloadSize,
                    PayloadChecksum: payloadChecksum,
                    Payload: payload
                );
            }
        }

        public static Message DecodeMessage(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeMessage(stream);
            }
        }

        public static void EncodeMessage(Stream stream, Message message)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteUInt32(message.Magic);
                writer.WriteFixedString(12, message.Command);
                writer.WriteUInt32(message.PayloadSize);
                writer.WriteUInt32(message.PayloadChecksum);
                writer.WriteBytes(message.PayloadSize.ToIntChecked(), message.Payload.ToArray());
            }
        }

        public static byte[] EncodeMessage(Message message)
        {
            using (var stream = new MemoryStream())
            {
                EncodeMessage(stream, message);
                return stream.ToArray();
            }
        }

        public static NetworkAddress DecodeNetworkAddress(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new NetworkAddress
                (
                    Services: reader.ReadUInt64(),
                    IPv6Address: reader.ReadBytes(16).ToImmutableArray(),
                    Port: reader.ReadUInt16BE()
                );
            }
        }

        public static NetworkAddress DecodeNetworkAddress(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeNetworkAddress(stream);
            }
        }

        public static void EncodeNetworkAddress(Stream stream, NetworkAddress networkAddress)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteUInt64(networkAddress.Services);
                writer.WriteBytes(16, networkAddress.IPv6Address.ToArray());
                writer.WriteUInt16BE(networkAddress.Port);
            }
        }

        public static byte[] EncodeNetworkAddress(NetworkAddress networkAddress)
        {
            using (var stream = new MemoryStream())
            {
                EncodeNetworkAddress(stream, networkAddress);
                return stream.ToArray();
            }
        }

        public static NetworkAddressWithTime DecodeNetworkAddressWithTime(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new NetworkAddressWithTime
                (
                    Time: reader.ReadUInt32(),
                    NetworkAddress: DecodeNetworkAddress(stream)
                );
            }
        }

        public static NetworkAddressWithTime DecodeNetworkAddressWithTime(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeNetworkAddressWithTime(stream);
            }
        }

        public static void EncodeNetworkAddressWithTime(Stream stream, NetworkAddressWithTime networkAddressWithTime)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteUInt32(networkAddressWithTime.Time);
                EncodeNetworkAddress(stream, networkAddressWithTime.NetworkAddress);
            }
        }

        public static byte[] EncodeNetworkAddressWithTime(NetworkAddressWithTime networkAddressWithTime)
        {
            using (var stream = new MemoryStream())
            {
                EncodeNetworkAddressWithTime(stream, networkAddressWithTime);
                return stream.ToArray();
            }
        }

        public static VersionPayload DecodeVersionPayload(Stream stream, int payloadLength)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                var position = stream.Position;

                var versionPayload = new VersionPayload
                (
                    ProtocolVersion: reader.ReadUInt32(),
                    ServicesBitfield: reader.ReadUInt64(),
                    UnixTime: reader.ReadUInt64(),
                    RemoteAddress: DecodeNetworkAddress(stream),
                    LocalAddress: DecodeNetworkAddress(stream),
                    Nonce: reader.ReadUInt64(),
                    UserAgent: reader.ReadVarString(),
                    StartBlockHeight: reader.ReadUInt32(),
                    Relay: false
                );

                var readCount = stream.Position - position;
                if (versionPayload.ProtocolVersion >= VersionPayload.RELAY_VERSION && payloadLength - readCount == 1)
                    versionPayload = versionPayload.With(Relay: reader.ReadBool());

                return versionPayload;
            }
        }

        public static VersionPayload DecodeVersionPayload(byte[] bytes, int payloadLength)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeVersionPayload(stream, payloadLength);
            }
        }

        public static void EncodeVersionPayload(Stream stream, VersionPayload versionPayload, bool withRelay)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteUInt32(versionPayload.ProtocolVersion);
                writer.WriteUInt64(versionPayload.ServicesBitfield);
                writer.WriteUInt64(versionPayload.UnixTime);
                EncodeNetworkAddress(stream, versionPayload.RemoteAddress);
                EncodeNetworkAddress(stream, versionPayload.LocalAddress);
                writer.WriteUInt64(versionPayload.Nonce);
                writer.WriteVarString(versionPayload.UserAgent);
                writer.WriteUInt32(versionPayload.StartBlockHeight);

                if (withRelay)
                    writer.WriteBool(versionPayload.Relay);
            }
        }

        public static byte[] EncodeVersionPayload(VersionPayload versionPayload, bool withRelay)
        {
            using (var stream = new MemoryStream())
            {
                EncodeVersionPayload(stream, versionPayload, withRelay);
                return stream.ToArray();
            }
        }

        public static NetworkAddressKey DecodeNetworkAddressKey(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return new NetworkAddressKey
                (
                    IPv6Address: reader.ReadVarBytes().ToImmutableArray(),
                    Port: reader.ReadUInt16()
                );
            }
        }

        public static NetworkAddressKey DecodeNetworkAddressKey(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DecodeNetworkAddressKey(stream);
            }
        }

        public static void EncodeNetworkAddressKey(Stream stream, NetworkAddressKey networkAddressKey)
        {
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                writer.WriteVarBytes(networkAddressKey.IPv6Address.ToArray());
                writer.Write(networkAddressKey.Port);
            }
        }

        public static byte[] EncodeNetworkAddressKey(NetworkAddressKey networkAddressKey)
        {
            using (var stream = new MemoryStream())
            {
                EncodeNetworkAddressKey(stream, networkAddressKey);
                return stream.ToArray();
            }
        }

        public static string DecodeVarString(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                return reader.ReadVarString();
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
                writer.WriteVarString(s);
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
    }
}
