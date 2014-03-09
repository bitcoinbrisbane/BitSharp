using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class BlockchainBuilder : IDisposable
    {
        //TODO use block hash instead of block metadata
        private ImmutableList<ChainedBlock>.Builder blockList;
        private ImmutableHashSet<UInt256>.Builder blockListHashes;
        private readonly UtxoBuilder utxoBuilder;

        public BlockchainBuilder(ImmutableList<ChainedBlock>.Builder blockList, ImmutableHashSet<UInt256>.Builder blockListHashes, UtxoBuilder utxoBuilder)
        {
            //Debug.Assert(!blockList.Where((x, i) => x.Height != i).Any());

            this.blockList = blockList;
            this.blockListHashes = blockListHashes;
            this.utxoBuilder = utxoBuilder;
        }

        ~BlockchainBuilder()
        {
            this.Dispose();
        }

        public ImmutableList<ChainedBlock>.Builder BlockList
        {
            get { return this.blockList; }
            set { this.blockList = value; }
        }

        public ImmutableHashSet<UInt256>.Builder BlockListHashes
        {
            get { return this.blockListHashes; }
            set { this.blockListHashes = value; }
        }

        public UtxoBuilder UtxoBuilder { get { return this.utxoBuilder; } }

        public int BlockCount { get { return this.BlockList.Count; } }

        public int Height { get { return this.BlockList.Count - 1; } }

        public BigInteger TotalWork { get { return this.RootBlock.TotalWork; } }

        public ChainedBlock RootBlock { get { return this.BlockList[this.BlockList.Count - 1]; } }

        public UInt256 RootBlockHash { get { return this.RootBlock.BlockHash; } }

        public override bool Equals(object obj)
        {
            if (!(obj is BlockchainBuilder))
                return false;

            return (BlockchainBuilder)obj == this;
        }

        public static bool operator ==(BlockchainBuilder left, BlockchainBuilder right)
        {
            return object.ReferenceEquals(left, right) || (!object.ReferenceEquals(left, null) && !object.ReferenceEquals(right, null) && left.BlockList.SequenceEqual(right.BlockList) && left.BlockListHashes.SetEquals(right.BlockListHashes)
                /*&& left.Utxo.SequenceEqual(right.Utxo, new UtxoComparer())*/);
        }

        public static bool operator !=(BlockchainBuilder left, BlockchainBuilder right)
        {
            return !(left == right);
        }

        public void Dispose()
        {
            this.utxoBuilder.Dispose();
            GC.SuppressFinalize(this);
        }

        private class UtxoComparer : IEqualityComparer<KeyValuePair<UInt256, UnspentTx>>
        {
            public bool Equals(KeyValuePair<UInt256, UnspentTx> x, KeyValuePair<UInt256, UnspentTx> y)
            {
                return x.Key == y.Key && x.Value == y.Value;
            }

            public int GetHashCode(KeyValuePair<UInt256, UnspentTx> obj)
            {
                return obj.Key.GetHashCode() ^ obj.Value.GetHashCode();
            }
        }
    }
}
