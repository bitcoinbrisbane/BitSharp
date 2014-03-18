using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class ChainedBlocks
    {
        private readonly ImmutableList<ChainedBlock> blockList;

        public ChainedBlocks(ImmutableList<ChainedBlock> blockList)
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

        public IEnumerable<Tuple<int, ChainedBlock>> NavigateTowards(ChainedBlocks targetChainedBlocks)
        {
            return this.NavigateTowards(() => targetChainedBlocks);
        }

        public IEnumerable<Tuple<int, ChainedBlock>> NavigateTowards(Func<ChainedBlocks> getTargetChainedBlocks)
        {
            var currentBlock = this.LastBlock;
            while (true)
            {
                var targetChainedBlocks = getTargetChainedBlocks();
                if (targetChainedBlocks == null)
                    yield break;
                else if (targetChainedBlocks.GenesisBlock != this.GenesisBlock)
                    throw new InvalidOperationException();

                var targetBlock = targetChainedBlocks.LastBlock;

                // if currently ahead of target chain, must rewind
                if (currentBlock.Height > targetChainedBlocks.Height)
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
                    if (targetChainedBlocks.BlockList[currentBlock.Height] == currentBlock)
                    {
                        // another block is available
                        if (targetChainedBlocks.Height >= currentBlock.Height + 1)
                        {
                            currentBlock = targetChainedBlocks.BlockList[currentBlock.Height + 1];
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

        public ChainedBlocksBuilder ToBuilder()
        {
            return new ChainedBlocksBuilder(this);
        }

        public static ChainedBlocks CreateForGenesisBlock(ChainedBlock genesisBlock)
        {
            return new ChainedBlocks(ImmutableList.Create<ChainedBlock>(genesisBlock));
        }
    }

    public class ChainedBlocksBuilder
    {
        private readonly ImmutableList<ChainedBlock>.Builder blockList;

        public ChainedBlocksBuilder(ChainedBlocks parentChainedBlocks)
        {
            this.blockList = parentChainedBlocks.BlockList.ToBuilder();
        }

        public ChainedBlock GenesisBlock { get { return this.blockList.First(); } }

        public ChainedBlock LastBlock { get { return this.blockList.Last(); } }

        public int Height { get { return this.blockList.Count() - 1; } }

        public ImmutableList<ChainedBlock> BlockList { get { return this.blockList.ToImmutable(); } }

        public IEnumerable<Tuple<int, ChainedBlock>> NavigateTowards(ChainedBlocks targetChainedBlocks)
        {
            return this.NavigateTowards(() => targetChainedBlocks);
        }

        public IEnumerable<Tuple<int, ChainedBlock>> NavigateTowards(Func<ChainedBlocks> getTargetChainedBlocks)
        {
            return this.ToImmutable().NavigateTowards(getTargetChainedBlocks);
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

        public ChainedBlocks ToImmutable()
        {
            return new ChainedBlocks(this.blockList.ToImmutable());
        }
    }
}
