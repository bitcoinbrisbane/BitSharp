using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core
{
    //TODO organize and name properly
    public static class DataCalculatorNew
    {
        public static IEnumerable<T> ReadBlockElements<T>(UInt256 blockHash, UInt256 merkleRoot, IEnumerable<T> blockElements)
            where T : BlockElement
        {
            var expectedIndex = 0;

            var merkleStream = new MerkleStream();

            foreach (var blockElement in blockElements)
            {
                if (blockElement.Index != expectedIndex)
                {
                    throw new ValidationException(blockHash);
                }

                merkleStream.AddHash(new MerkleHash(blockElement));

                yield return blockElement;

                expectedIndex += 1 << blockElement.Depth;
            }

            merkleStream.FinishPairing();

            if (merkleStream.RootHash.Hash != merkleRoot)
            {
                throw new ValidationException(blockHash);
            }
        }

        public static UInt256 PairHashes(UInt256 left, UInt256 right)
        {
            var bytes = ImmutableArray.CreateBuilder<byte>(64);
            bytes.AddRange(left.ToByteArray());
            bytes.AddRange(right.ToByteArray());
            return new UInt256(new SHA256Managed().ComputeDoubleHash(bytes.ToArray()));
        }

        private class MerkleStream
        {
            private readonly SHA256Managed sha256 = new SHA256Managed();
            private readonly List<MerkleHash> leftHashes = new List<MerkleHash>();

            public MerkleHash RootHash
            {
                get
                {
                    if (this.leftHashes.Count != 1)
                        throw new InvalidOperationException();

                    return this.leftHashes[0];
                }
            }

            public void AddHash(MerkleHash newHash)
            {
                if (this.leftHashes.Count == 0)
                {
                    this.leftHashes.Add(newHash);
                }
                else
                {
                    var leftHash = this.leftHashes.Last();

                    if (newHash.Depth < leftHash.Depth)
                    {
                        this.leftHashes.Add(newHash);
                    }
                    else if (newHash.Depth == leftHash.Depth)
                    {
                        this.leftHashes[this.leftHashes.Count - 1] = Pair(leftHash, newHash);
                    }
                    else if (newHash.Depth > leftHash.Depth)
                    {
                        throw new InvalidOperationException();
                    }

                    this.ClosePairs();
                }
            }

            public void FinishPairing()
            {
                if (this.leftHashes.Count == 0)
                    throw new InvalidOperationException();

                while (this.leftHashes.Count > 1)
                {
                    AddHash(this.leftHashes.Last());
                }
            }

            private void ClosePairs()
            {
                while (this.leftHashes.Count >= 2)
                {
                    var leftHash = this.leftHashes[this.leftHashes.Count - 2];
                    var rightHash = this.leftHashes[this.leftHashes.Count - 1];

                    if (leftHash.Depth == rightHash.Depth)
                    {
                        this.leftHashes.RemoveAt(this.leftHashes.Count - 1);
                        this.leftHashes[this.leftHashes.Count - 1] = Pair(leftHash, rightHash);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            private MerkleHash Pair(MerkleHash left, MerkleHash right)
            {
                if (left.Depth != right.Depth)
                    throw new InvalidOperationException();

                var pairHashBytes = ImmutableArray.CreateBuilder<byte>(64);
                pairHashBytes.AddRange(left.Hash.ToByteArray());
                pairHashBytes.AddRange(right.Hash.ToByteArray());

                var pairHash = new UInt256(this.sha256.ComputeDoubleHash(pairHashBytes.ToArray()));

                return new MerkleHash(left.Depth + 1, pairHash);
            }


            private UInt256 PairHashes(UInt256 left, UInt256 right)
            {
                var bytes = ImmutableArray.CreateBuilder<byte>(64);
                bytes.AddRange(left.ToByteArray());
                bytes.AddRange(right.ToByteArray());
                return new UInt256(new SHA256Managed().ComputeDoubleHash(bytes.ToArray()));
            }
        }

        private struct MerkleHash
        {
            private readonly int depth;
            private readonly UInt256 hash;

            public MerkleHash(int depth, UInt256 hash)
            {
                this.depth = depth;
                this.hash = hash;
            }

            public MerkleHash(BlockElement blockElement)
            {
                this.depth = blockElement.Depth;
                this.hash = blockElement.Hash;
            }

            public int Depth { get { return this.depth; } }

            public UInt256 Hash { get { return this.hash; } }
        }
    }
}
