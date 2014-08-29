using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class ChainBuilder
    {
        private readonly ImmutableList<ChainedHeader>.Builder blocks;
        private readonly ImmutableDictionary<UInt256, ChainedHeader>.Builder blocksByHash;

        public ChainBuilder()
        {
            this.blocks = ImmutableList.CreateBuilder<ChainedHeader>();
            this.blocksByHash = ImmutableDictionary.CreateBuilder<UInt256, ChainedHeader>();
        }

        public ChainBuilder(Chain parentChain)
        {
            this.blocks = parentChain.Blocks.ToBuilder();
            this.blocksByHash = parentChain.BlocksByHash.ToBuilder();
        }

        public ChainBuilder(IEnumerable<ChainedHeader> chainedHeaders)
        {
            this.blocks = ImmutableList.CreateBuilder<ChainedHeader>();
            this.blocksByHash = ImmutableDictionary.CreateBuilder<UInt256, ChainedHeader>();

            foreach (var chainedHeader in chainedHeaders)
                this.AddBlock(chainedHeader);
        }

        public ChainedHeader GenesisBlock { get { return this.blocks.FirstOrDefault(); } }

        public ChainedHeader LastBlock { get { return this.blocks.LastOrDefault(); } }

        public int Height { get { return this.blocks.Count() - 1; } }

        public BigInteger TotalWork { get { return this.LastBlock != null ? this.LastBlock.TotalWork : 0; } }

        public ImmutableList<ChainedHeader> Blocks { get { return this.blocks.ToImmutable(); } }

        public ImmutableDictionary<UInt256, ChainedHeader> BlocksByHash { get { return this.blocksByHash.ToImmutable(); } }

        public void AddBlock(ChainedHeader chainedHeader)
        {
            var lastBlock = this.LastBlock;
            if (lastBlock != null
                && (chainedHeader.PreviousBlockHash != lastBlock.Hash
                    || chainedHeader.Height != lastBlock.Height + 1))
                throw new InvalidOperationException();

            this.blocks.Add(chainedHeader);
            this.blocksByHash.Add(chainedHeader.Hash, chainedHeader);
        }

        public void RemoveBlock(ChainedHeader chainedHeader)
        {
            var lastBlock = this.LastBlock;
            if (lastBlock == null
                || chainedHeader != lastBlock
                || this.blocks.Count == 0)
                throw new InvalidOperationException();

            this.blocks.RemoveAt(this.blocks.Count - 1);
            this.blocksByHash.Remove(chainedHeader.Hash);
        }

        public Chain ToImmutable()
        {
            return new Chain(this.blocks.ToImmutable(), this.blocksByHash.ToImmutable());
        }
    }
}
