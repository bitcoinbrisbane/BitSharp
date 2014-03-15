using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class BlockchainPath
    {
        private readonly ChainedBlock fromBlock;
        private readonly ChainedBlock toBlock;
        private readonly ChainedBlock lastCommonBlock;
        private readonly IImmutableList<ChainedBlock> rewindBlocks;
        private readonly IImmutableList<ChainedBlock> advanceBlocks;

        public BlockchainPath(ChainedBlock fromBlock, ChainedBlock toBlock, ChainedBlock lastCommonBlock, IImmutableList<ChainedBlock> rewindBlocks, IImmutableList<ChainedBlock> advanceBlocks)
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

        public IImmutableList<ChainedBlock> RewindBlocks { get { return this.rewindBlocks; } }

        public IImmutableList<ChainedBlock> AdvanceBlocks { get { return this.advanceBlocks; } }
    }
}
