using BitSharp.Common;
using BitSharp.Core.Domain.Builders;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class Chain
    {
        private readonly ImmutableList<ChainedBlock> blocks;

        public Chain(ImmutableList<ChainedBlock> blocks)
        {
            if (blocks == null)
                throw new ArgumentNullException("blocks");

            if (blocks.Count == 0
                || blocks[0].Height != 0)
                throw new InvalidOperationException();

            this.blocks = blocks;
        }

        public ChainedBlock GenesisBlock { get { return this.blocks.First(); } }

        public ChainedBlock LastBlock { get { return this.blocks.Last(); } }

        public int Height { get { return this.blocks.Count() - 1; } }

        public ImmutableList<ChainedBlock> Blocks { get { return this.blocks; } }

        public IEnumerable<ChainedBlock> ReadFromGenesis()
        {
            var expectedHeight = 0;
            var expectedPrevBlockHash = this.GenesisBlock.PreviousBlockHash;
            foreach (var block in this.blocks)
            {
                if (block.Height != expectedHeight
                    || block.PreviousBlockHash != expectedPrevBlockHash)
                    throw new InvalidOperationException();

                yield return block;
                expectedHeight++;
                expectedPrevBlockHash = block.PreviousBlockHash;
            }
        }

        public IEnumerable<Tuple<int, ChainedBlock>> NavigateTowards(Chain targetChain)
        {
            return this.NavigateTowards(() => targetChain);
        }

        public IEnumerable<Tuple<int, ChainedBlock>> NavigateTowards(Func<Chain> getTargetChain)
        {
            var currentBlock = this.LastBlock;
            while (true)
            {
                var targetChain = getTargetChain();
                if (targetChain == null)
                    yield break;
                else if (targetChain.GenesisBlock != this.GenesisBlock)
                    throw new InvalidOperationException();

                var targetBlock = targetChain.LastBlock;

                // if currently ahead of target chain, must rewind
                if (currentBlock.Height > targetChain.Height)
                {
                    if (currentBlock.Height == 0)
                        throw new InvalidOperationException();

                    yield return Tuple.Create(-1, currentBlock);
                    currentBlock = this.blocks[currentBlock.Height - 1];
                }
                // currently behind target chain
                else
                {
                    // on same chain, can advance
                    if (targetChain.Blocks[currentBlock.Height] == currentBlock)
                    {
                        // another block is available
                        if (targetChain.Height >= currentBlock.Height + 1)
                        {
                            currentBlock = targetChain.Blocks[currentBlock.Height + 1];
                            yield return Tuple.Create(+1, currentBlock);
                        }
                        // no further blocks are available
                        else
                        {
                            yield break;
                        }
                    }
                    // on different chains, must rewind
                    else
                    {
                        if (currentBlock.Height == 0)
                            throw new InvalidOperationException();

                        yield return Tuple.Create(-1, currentBlock);
                        currentBlock = this.blocks[currentBlock.Height - 1];
                    }
                }
            }
        }

        public ChainBuilder ToBuilder()
        {
            return new ChainBuilder(this);
        }

        public static Chain CreateForGenesisBlock(ChainedBlock genesisBlock)
        {
            return new Chain(ImmutableList.Create<ChainedBlock>(genesisBlock));
        }
    }
}
