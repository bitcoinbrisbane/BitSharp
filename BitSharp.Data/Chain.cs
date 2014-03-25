using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class Chain
    {
        private readonly ImmutableList<ChainedBlock> blockList;

        public Chain(ImmutableList<ChainedBlock> blockList)
        {
            if (blockList.Count == 0
                || blockList[0].Height != 0)
                throw new InvalidOperationException();

            this.blockList = blockList;
        }

        public ChainedBlock GenesisBlock { get { return this.blockList.First(); } }

        public ChainedBlock LastBlock { get { return this.blockList.Last(); } }

        public int Height { get { return this.blockList.Count() - 1; } }

        public ImmutableList<ChainedBlock> BlockList { get { return this.blockList; } }

        public IEnumerable<ChainedBlock> ReadFromGenesis()
        {
            var expectedHeight = 0;
            var expectedPrevBlockHash = this.GenesisBlock.PreviousBlockHash;
            foreach (var block in this.blockList)
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
                    currentBlock = this.blockList[currentBlock.Height - 1];
                }
                // currently behind target chain
                else
                {
                    // on same chain, can advance
                    if (targetChain.BlockList[currentBlock.Height] == currentBlock)
                    {
                        // another block is available
                        if (targetChain.Height >= currentBlock.Height + 1)
                        {
                            currentBlock = targetChain.BlockList[currentBlock.Height + 1];
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
                        currentBlock = this.blockList[currentBlock.Height - 1];
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
