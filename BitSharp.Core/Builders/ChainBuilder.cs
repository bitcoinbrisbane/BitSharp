﻿using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class ChainBuilder
    {
        private readonly ImmutableList<ChainedHeader>.Builder blocks;

        public ChainBuilder()
        {
            this.blocks = ImmutableList.CreateBuilder<ChainedHeader>();
        }

        public ChainBuilder(Chain parentChain)
        {
            this.blocks = parentChain.Blocks.ToBuilder();
        }

        public ChainedHeader GenesisBlock { get { return this.blocks.First(); } }

        public ChainedHeader LastBlock { get { return this.blocks.Last(); } }

        public UInt256 LastBlockHash { get { return this.LastBlock.Hash; } }

        public int Height { get { return this.blocks.Count() - 1; } }

        public ImmutableList<ChainedHeader> Blocks { get { return this.blocks.ToImmutable(); } }

        public IEnumerable<Tuple<int, ChainedHeader>> NavigateTowards(Chain targetChain)
        {
            return this.NavigateTowards(() => targetChain);
        }

        public IEnumerable<Tuple<int, ChainedHeader>> NavigateTowards(Func<Chain> getTargetChain)
        {
            return this.ToImmutable().NavigateTowards(getTargetChain);
        }

        public void AddBlock(ChainedHeader chainedHeader)
        {
            var lastBlock = this.blocks.LastOrDefault();
            if (lastBlock != null
                && (chainedHeader.PreviousBlockHash != lastBlock.Hash
                    || chainedHeader.Height != lastBlock.Height + 1))
                throw new InvalidOperationException();

            this.blocks.Add(chainedHeader);
        }

        public void RemoveBlock(ChainedHeader chainedHeader)
        {
            var lastBlock = this.LastBlock;
            if (chainedHeader != lastBlock
                || this.blocks.Count == 0)
                throw new InvalidOperationException();

            this.blocks.RemoveAt(this.blocks.Count - 1);
        }

        public Chain ToImmutable()
        {
            return new Chain(this.blocks.ToImmutable());
        }
    }
}
