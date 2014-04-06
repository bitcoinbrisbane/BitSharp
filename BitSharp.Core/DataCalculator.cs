using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core
{
    public static class DataCalculator
    {
        public static UInt256 CalculateBlockHash(BlockHeader blockHeader)
        {
            return new UInt256(Crypto.DoubleSHA256(DataEncoder.EncodeBlockHeader(blockHeader)));
        }

        public static UInt256 CalculateBlockHash(UInt32 Version, UInt256 PreviousBlock, UInt256 MerkleRoot, UInt32 Time, UInt32 Bits, UInt32 Nonce)
        {
            return new UInt256(Crypto.DoubleSHA256(DataEncoder.EncodeBlockHeader(Version, PreviousBlock, MerkleRoot, Time, Bits, Nonce)));
        }

        public static UInt256 CalculateTransactionHash(Transaction tx)
        {
            return new UInt256(Crypto.DoubleSHA256(DataEncoder.EncodeTransaction(tx)));
        }

        public static UInt256 CalculateTransactionHash(UInt32 Version, ImmutableArray<TxInput> Inputs, ImmutableArray<TxOutput> Outputs, UInt32 LockTime)
        {
            return new UInt256(Crypto.DoubleSHA256(DataEncoder.EncodeTransaction(Version, Inputs, Outputs, LockTime)));
        }

        public static UInt256 CalculateMerkleRoot(IImmutableList<UInt256> txHashes)
        {
            ImmutableList<ImmutableArray<byte>> merkleTree;
            return CalculateMerkleRoot(txHashes, out merkleTree);
        }

        public static UInt256 CalculateMerkleRoot(IImmutableList<UInt256> txHashes, out ImmutableList<ImmutableArray<byte>> merkleTree)
        {
            var workingMerkleTree = ImmutableList.CreateBuilder<ImmutableArray<byte>>();

            var hashes = txHashes.Select(txHash => txHash.ToByteArray().ToImmutableArray()).ToList();

            workingMerkleTree.AddRange(hashes);
            while (hashes.Count > 1)
            {
                workingMerkleTree.AddRange(hashes);

                // ensure row is even length
                if (hashes.Count % 2 != 0)
                    hashes.Add(hashes.Last());

                // pair up hashes in row ({1, 2, 3, 4} into {{1, 2}, {3, 4}}) and then hash the pairs
                // the result is the next row, which will be half the size of the current row
                hashes =
                    Enumerable.Range(0, hashes.Count / 2)
                    .Select(i => hashes[i * 2].AddRange(hashes[i * 2 + 1]))
                    //.AsParallel().AsOrdered().WithExecutionMode(ParallelExecutionMode.ForceParallelism).WithDegreeOfParallelism(10)
                    .Select(pair => Crypto.DoubleSHA256(pair.ToArray()).ToImmutableArray())
                    .ToList();
            }
            Debug.Assert(hashes.Count == 1);

            merkleTree = workingMerkleTree.ToImmutable();
            return new UInt256(hashes[0].ToArray());
        }

        //TDOO name...
        private static readonly BigInteger Max256BitTarget = BigInteger.Pow(2, 256);
        public static BigInteger CalculateWork(BlockHeader blockHeader)
        {
            try
            {
                return Max256BitTarget / (BigInteger)BitsToTarget(blockHeader.Bits);
            }
            catch (Exception)
            {
                Debug.WriteLine("Corrupt block header bits: {0}, block {1}".Format2(blockHeader.Bits.ToString("X"), blockHeader.Hash.ToHexNumberString()));
                return -1;
            }
        }

        public static UInt256 BitsToTarget(UInt32 bits)
        {
            // last three bytes store the multiplicand
            var multiplicand = (UInt256)bits % 0x1000000;
            if (multiplicand > 0x7fffff)
                throw new ArgumentOutOfRangeException("bits");

            // first byte stores the value to be used in the power
            var powerPart = (int)(bits >> 24);
            var multiplier = new UInt256(BigInteger.Pow(2, 8 * (powerPart - 3)));

            return multiplicand * multiplier;
        }

        public static UInt32 TargetToBits(UInt256 target)
        {
            // to get the powerPart: take the log in base 2, round up to 8 to respect byte boundaries, and then remove 24 to represent 3 bytes of precision
            var log = Math.Ceiling(UInt256.Log(target, 2) / 8) * 8 - 24;
            var powerPart = (byte)(log / 8 + 3);

            // determine the multiplier based on the powerPart
            var multiplier = BigInteger.Pow(2, 8 * (powerPart - 3));

            // to get multiplicand: divide the target by the multiplier
            //TODO
            var multiplicandBytes = ((BigInteger)target / (BigInteger)multiplier).ToByteArray();
            Debug.Assert(multiplicandBytes.Length == 3 || multiplicandBytes.Length == 4);

            // this happens when multipicand would be greater than 0x7fffff
            // TODO need a better explanation comment
            if (multiplicandBytes.Last() == 0)
            {
                multiplicandBytes = multiplicandBytes.Skip(1).ToArray();
                powerPart++;
            }

            // construct the bits representing the powerPart and multiplicand
            var bits = Bits.ToUInt32(multiplicandBytes.Concat(powerPart).ToArray());
            return bits;
        }
    }
}
