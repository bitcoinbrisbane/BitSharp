using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    public class RandomDataOptions
    {
        public int? MinimumBlockCount { get; set; }
        public int? BlockCount { get; set; }
        public int? TransactionCount { get; set; }
        public int? TxInputCount { get; set; }
        public int? TxOutputCount { get; set; }
        public int? ScriptSignatureSize { get; set; }
        public int? ScriptPublicKeySize { get; set; }
    }

    public static class RandomData
    {
        private static readonly Random random = new Random();

        public static Block RandomBlock(RandomDataOptions options = default(RandomDataOptions))
        {
            return new Block
            (
                header: RandomBlockHeader(),
                transactions: Enumerable.Range(0, random.Next((options != null ? options.TransactionCount : null) ?? 100)).Select(x => RandomTransaction()).ToImmutableList()
            );
        }

        public static BlockHeader RandomBlockHeader(RandomDataOptions options = default(RandomDataOptions))
        {
            return new BlockHeader
            (
                version: random.NextUInt32(),
                previousBlock: random.NextUInt256(),
                merkleRoot: random.NextUInt256(),
                time: random.NextUInt32(),
                bits: random.NextUInt32(),
                nonce: random.NextUInt32()
            );
        }

        public static Transaction RandomTransaction(RandomDataOptions options = default(RandomDataOptions))
        {
            return new Transaction
            (
                version: random.NextUInt32(),
                inputs: Enumerable.Range(0, random.Next((options != null ? options.TxInputCount : null) ?? 100)).Select(x => RandomTxInput()).ToImmutableList(),
                outputs: Enumerable.Range(0, random.Next((options != null ? options.TxOutputCount : null) ?? 100)).Select(x => RandomTxOutput()).ToImmutableList(),
                lockTime: random.NextUInt32()
            );
        }

        public static TxInput RandomTxInput(RandomDataOptions options = default(RandomDataOptions))
        {
            return new TxInput
            (
                previousTxOutputKey: new TxOutputKey
                (
                    txHash: random.NextUInt32(),
                    txOutputIndex: random.NextUInt32()
                ),
                scriptSignature: random.NextBytes(random.Next((options != null ? options.ScriptSignatureSize : null) ?? 100)).ToImmutableList(),
                sequence: random.NextUInt32()
            );
        }

        public static TxOutput RandomTxOutput(RandomDataOptions options = default(RandomDataOptions))
        {
            return new TxOutput
            (
                value: random.NextUInt64(),
                scriptPublicKey: random.NextBytes(random.Next((options != null ? options.ScriptPublicKeySize : null) ?? 100)).ToImmutableList()
            );
        }

        //public static Blockchain RandomBlockchain(RandomDataOptions options = default(RandomDataOptions))
        //{
        //    //TODO blockCount algorithm isn't exact
        //    var blockCount = random.Next((options != null ? options.BlockCount : null) ?? 100) + ((options != null ? options.MinimumBlockCount : null) ?? 1);
        //    var blockList = new List<Block>(blockCount);
        //    var chainedBlockList = new List<ChainedBlock>(blockCount);

        //    var previousBlockHash = UInt256.Zero;
        //    var totalWork = new BigInteger(0);
        //    for (var i = 0; i < blockCount; i++)
        //    {
        //        var block = RandomData.RandomBlock(options);
        //        block = block.With(Header: block.Header.With(PreviousBlock: previousBlockHash));
        //        blockList.Add(block);

        //        previousBlockHash = block.Hash;
        //        totalWork += block.Header.CalculateWork();

        //        chainedBlockList.Add(new ChainedBlock(block.Hash, block.Header.PreviousBlock, i, totalWork));
        //    }

        //    var blockListHashes = blockList.Select(x => x.Hash).ToImmutableHashSet();
        //    var utxo = blockList.SelectMany(block =>
        //        block.Transactions.Select((tx, txIndex) =>
        //            new UnspentTx(tx.Hash, random.NextImmutableBitArray((options != null ? options.TxOutputCount : null) ?? 100))))
        //        .ToImmutableDictionary(unspentTx => unspentTx.TxHash, unspentTx => unspentTx);

        //    return new Blockchain
        //    (
        //        chainedBlockList.ToImmutableList(),
        //        blockListHashes,
        //        null //TODO new MemoryUtxo(blockListHashes.Last(), utxo)
        //    );
        //}

        public static BlockchainKey RandomBlockchainKey()
        {
            return new BlockchainKey
            (
                guid: Guid.NewGuid(),
                rootBlockHash: random.NextUInt256()
            );
        }

        public static BlockchainMetadata RandomBlockchainMetadata()
        {
            return new BlockchainMetadata
            (
                guid: Guid.NewGuid(),
                rootBlockHash: random.NextUInt256(),
                totalWork: random.NextUBigIntegerBytes(64)
            );
        }

        public static ChainedBlock RandomChainedBlock()
        {
            return new ChainedBlock
            (
                blockHash: random.NextUInt256(),
                previousBlockHash: random.NextUInt256(),
                height: Math.Abs(random.Next()),
                totalWork: random.NextUBigIntegerBytes(64)
            );
        }

        public static UnspentTx RandomUnspentTx(RandomDataOptions options = default(RandomDataOptions))
        {
            return new UnspentTx
            (
                txHash: random.NextUInt256(),
                unspentOutputs: random.NextImmutableBitArray(random.Next((options != null ? options.TxOutputCount : null) ?? 100))
            );
        }

        public static TxOutputKey RandomTxOutputKey()
        {
            return new TxOutputKey
            (
                txHash: random.NextUInt256(),
                txOutputIndex: random.NextUInt32()
            );
        }

        public static ImmutableBitArray NextImmutableBitArray(this Random random, int length)
        {
            var bitArray = new BitArray(length);
            for (var i = 0; i < length; i++)
                bitArray[i] = random.NextBool();

            return bitArray.ToImmutableBitArray();
        }
    }
}
