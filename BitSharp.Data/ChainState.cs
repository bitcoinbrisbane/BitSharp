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
        //private readonly ChainedBlock targetBlock;
        //private readonly BlockchainPath targetBlockPath;

        public ChainState(Blockchain currentBlock)
        {
            this.currentBlock = currentBlock;
            //this.targetBlock = currentBlock.RootBlock;
            //this.targetBlockPath = new BlockchainPath(currentBlock.RootBlock, currentBlock.RootBlock, currentBlock.RootBlock, ImmutableList.Create<ChainedBlock>(), ImmutableList.Create<ChainedBlock>());
        }

        //public ChainState(Blockchain currentBlock, BlockchainPath targetBlockPath)
        //{
        //    this.currentBlock = currentBlock;
        //    //this.targetBlockPath = targetBlockPath;
        //}

        public Blockchain CurrentBlock { get { return this.currentBlock; } }

        //public ChainedBlock TargetBlock { get { return this.targetBlockPath.ToBlock; } }

        //public BlockchainPath TargetBlockPath { get { return this.targetBlockPath; } }
    }
}
