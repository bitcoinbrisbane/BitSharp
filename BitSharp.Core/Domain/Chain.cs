using BitSharp.Common;
using BitSharp.Core.Builders;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class Chain
    {
        private readonly ImmutableList<ChainedHeader> blocks;
        private readonly ImmutableDictionary<UInt256, ChainedHeader> blocksByHash;

        internal Chain(ImmutableList<ChainedHeader> blocks, ImmutableDictionary<UInt256, ChainedHeader> blocksByHash)
        {
            if (blocks == null)
                throw new ArgumentNullException("blocks");
            if (blocksByHash == null)
                throw new ArgumentNullException("blocksByHash");
            if (blocks.Count != blocksByHash.Count)
                throw new ArgumentException();

            this.blocks = blocks;
            this.blocksByHash = blocksByHash;
        }

        public ChainedHeader GenesisBlock { get { return this.blocks.First(); } }

        public ChainedHeader LastBlock { get { return this.blocks.Last(); } }

        public int Height { get { return this.blocks.Count() - 1; } }

        public BigInteger TotalWork { get { return this.LastBlock.TotalWork; } }

        public ImmutableList<ChainedHeader> Blocks { get { return this.blocks; } }

        public ImmutableDictionary<UInt256, ChainedHeader> BlocksByHash { get { return this.blocksByHash; } }

        public IEnumerable<ChainedHeader> ReadFromGenesis()
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
                expectedPrevBlockHash = block.Hash;
            }
        }

        public IEnumerable<Tuple<int, ChainedHeader>> NavigateTowards(Chain targetChain)
        {
            return this.NavigateTowards(() => targetChain);
        }

        public IEnumerable<Tuple<int, ChainedHeader>> NavigateTowards(Func<Chain> getTargetChain)
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

        public static Chain CreateForGenesisBlock(ChainedHeader genesisBlock)
        {
            var chainBuilder = new ChainBuilder();
            chainBuilder.AddBlock(genesisBlock);
            return chainBuilder.ToImmutable();
        }
    }
}
