using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class BlockchainPath
    {
        private readonly ChainedHeader fromBlock;
        private readonly ChainedHeader toBlock;
        private readonly ChainedHeader lastCommonBlock;
        private readonly ImmutableList<ChainedHeader> rewindBlocks;
        private readonly ImmutableList<ChainedHeader> advanceBlocks;

        public BlockchainPath(ChainedHeader fromBlock, ChainedHeader toBlock, ChainedHeader lastCommonBlock, ImmutableList<ChainedHeader> rewindBlocks, ImmutableList<ChainedHeader> advanceBlocks)
        {
            this.fromBlock = fromBlock;
            this.toBlock = toBlock;
            this.lastCommonBlock = lastCommonBlock;
            this.rewindBlocks = rewindBlocks;
            this.advanceBlocks = advanceBlocks;
        }

        public ChainedHeader FromBlock { get { return this.fromBlock; } }

        public ChainedHeader ToBlock { get { return this.toBlock; } }

        public ChainedHeader LastCommonBlock { get { return this.lastCommonBlock; } }

        public ImmutableList<ChainedHeader> RewindBlocks { get { return this.rewindBlocks; } }

        public ImmutableList<ChainedHeader> AdvanceBlocks { get { return this.advanceBlocks; } }

        public static BlockchainPath CreateSingleBlockPath(ChainedHeader chainedHeader)
        {
            return new BlockchainPath(chainedHeader, chainedHeader, chainedHeader, ImmutableList.Create<ChainedHeader>(), ImmutableList.Create<ChainedHeader>());
        }
    }
}
