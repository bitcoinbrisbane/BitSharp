using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain.Builders
{
    public class ChainBuilder
    {
        private readonly ConcurrentListBuilder<ChainedBlock> blocks;

        public ChainBuilder(Chain parentChain)
        {
            this.blocks = new ConcurrentListBuilder<ChainedBlock>(parentChain.Blocks);
        }

        public ChainedBlock GenesisBlock { get { return this.blocks.First(); } }

        public ChainedBlock LastBlock { get { return this.blocks.Last(); } }

        public int Height { get { return this.blocks.Count() - 1; } }

        public ImmutableList<ChainedBlock> Blocks { get { return this.blocks.ToImmutable(); } }

        public IEnumerable<Tuple<int, ChainedBlock>> NavigateTowards(Chain targetChain)
        {
            return this.NavigateTowards(() => targetChain);
        }

        public IEnumerable<Tuple<int, ChainedBlock>> NavigateTowards(Func<Chain> getTargetChain)
        {
            return this.ToImmutable().NavigateTowards(getTargetChain);
        }

        public void AddBlock(ChainedBlock block)
        {
            var lastBlock = this.LastBlock;
            if (block.PreviousBlockHash != lastBlock.BlockHash
                || block.Height != lastBlock.Height + 1)
                throw new InvalidOperationException();

            this.blocks.Add(block);
        }

        public void RemoveBlock(ChainedBlock block)
        {
            var lastBlock = this.LastBlock;
            if (block != lastBlock
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
