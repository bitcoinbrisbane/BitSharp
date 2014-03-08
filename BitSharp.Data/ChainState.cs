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
        private readonly ChainedBlock currentBlock;
        private readonly ChainedBlock targetBlock;
        private readonly IImmutableList<ChainedBlock> forwardBlocks;
        private readonly IImmutableList<ChainedBlock> rewindBlocks;

        public ChainState(ChainedBlock currentBlock, ChainedBlock targetBlock, IImmutableList<ChainedBlock> forwardBlocks, IImmutableList<ChainedBlock> rewindBlocks)
        {
            this.currentBlock = currentBlock;
            this.targetBlock = targetBlock;
            this.forwardBlocks = forwardBlocks;
            this.rewindBlocks = rewindBlocks;
        }

        public ChainedBlock CurrentBlock { get { return this.currentBlock; } }

        public ChainedBlock TargetBlock { get { return this.targetBlock; } }

        public IImmutableList<ChainedBlock> ForwardBlocks { get { return this.forwardBlocks; } }

        public IImmutableList<ChainedBlock> RewindBlocks { get { return this.rewindBlocks; } }
    }
}
