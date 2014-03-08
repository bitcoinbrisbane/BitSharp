using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class ChainState
    {
        private readonly Blockchain currentBlock;
        private readonly ChainedBlock targetBlock;
        private readonly ImmutableList<ChainedBlock> targetBlockchain;
        private readonly ImmutableList<ChainedBlock> rewindBlocks;
        private readonly ImmutableList<ChainedBlock> forwardBlocks;

        public ChainState(Blockchain currentBlock)
        {
            this.currentBlock = currentBlock;
            this.targetBlock = currentBlock.RootBlock;
            this.targetBlockchain = currentBlock.BlockList;
            this.rewindBlocks = ImmutableList.Create<ChainedBlock>();
            this.forwardBlocks = ImmutableList.Create<ChainedBlock>();
        }

        public ChainState(Blockchain currentBlock, ChainedBlock targetBlock, ImmutableList<ChainedBlock> targetBlockchain, ImmutableList<ChainedBlock> rewindBlocks)
        {
            this.currentBlock = currentBlock;
            this.targetBlock = targetBlock;
            this.targetBlockchain = targetBlockchain;
            this.rewindBlocks = rewindBlocks;
            this.forwardBlocks = targetBlockchain.Skip(currentBlock.BlockList.Count - this.rewindBlocks.Count).ToImmutableList();
        }

        public Blockchain CurrentBlock { get { return this.currentBlock; } }

        public ChainedBlock TargetBlock { get { return this.targetBlock; } }

        public ImmutableList<ChainedBlock> TargetBlockchain { get { return this.targetBlockchain; } }

        public ImmutableList<ChainedBlock> RewindBlocks { get { return this.rewindBlocks; } }

        public ImmutableList<ChainedBlock> ForwardBlocks { get { return this.forwardBlocks; } }
    }
}
