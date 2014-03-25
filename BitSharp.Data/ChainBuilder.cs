using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class ChainBuilder
    {
        private readonly ImmutableList<ChainedBlock>.Builder blockList;

        public ChainBuilder(Chain parentChain)
        {
            this.blockList = parentChain.BlockList.ToBuilder();
        }

        public ChainedBlock GenesisBlock { get { return this.blockList.First(); } }

        public ChainedBlock LastBlock { get { return this.blockList.Last(); } }

        public int Height { get { return this.blockList.Count() - 1; } }

        public ImmutableList<ChainedBlock> BlockList { get { return this.blockList.ToImmutable(); } }

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

            this.blockList.Add(block);
        }

        public void RemoveBlock(ChainedBlock block)
        {
            var lastBlock = this.LastBlock;
            if (block != lastBlock
                || this.blockList.Count == 0)
                throw new InvalidOperationException();

            this.blockList.RemoveAt(this.blockList.Count - 1);
        }

        public Chain ToImmutable()
        {
            return new Chain(this.blockList.ToImmutable());
        }
    }
}
