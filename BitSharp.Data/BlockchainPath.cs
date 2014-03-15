using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class BlockchainPath
    {
        private readonly ChainedBlock fromBlock;
        private readonly ChainedBlock toBlock;
        private readonly ChainedBlock lastCommonBlock;
        private readonly ImmutableList<ChainedBlock> rewindBlocks;
        private readonly ImmutableList<ChainedBlock> advanceBlocks;

        public BlockchainPath(ChainedBlock fromBlock, ChainedBlock toBlock, ChainedBlock lastCommonBlock, ImmutableList<ChainedBlock> rewindBlocks, ImmutableList<ChainedBlock> advanceBlocks)
        {
            this.fromBlock = fromBlock;
            this.toBlock = toBlock;
            this.lastCommonBlock = lastCommonBlock;
            this.rewindBlocks = rewindBlocks;
            this.advanceBlocks = advanceBlocks;
        }

        public ChainedBlock FromBlock { get { return this.fromBlock; } }

        public ChainedBlock ToBlock { get { return this.toBlock; } }

        public ChainedBlock LastCommonBlock { get { return this.lastCommonBlock; } }

        public ImmutableList<ChainedBlock> RewindBlocks { get { return this.rewindBlocks; } }

        public ImmutableList<ChainedBlock> AdvanceBlocks { get { return this.advanceBlocks; } }

        public static BlockchainPath CreateSingleBlockPath(ChainedBlock block)
        {
            return new BlockchainPath(block, block, block, ImmutableList.Create<ChainedBlock>(), ImmutableList.Create<ChainedBlock>());
        }
    }
}
