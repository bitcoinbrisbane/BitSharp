using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class ChainState
    {
        private readonly ChainedBlocks currentChainedBlocks;
        private readonly IUtxoStorage currentUtxo;

        public ChainState(ChainedBlocks currentChainedBlocks, IUtxoStorage currentUtxo)
        {
            this.currentChainedBlocks = currentChainedBlocks;
            this.currentUtxo = currentUtxo;
        }

        public ChainedBlocks CurrentChainedBlocks { get { return this.currentChainedBlocks; } }

        public IUtxoStorage CurrentUtxo { get { return this.currentUtxo; } }

        public ChainedBlock CurrentBlock { get { return this.currentChainedBlocks.LastBlock; } }

        public static ChainState CreateForGenesisBlock(ChainedBlock genesisBlock)
        {
            return new ChainState(
                ChainedBlocks.CreateForGenesisBlock(genesisBlock),
                new GenesisUtxo(genesisBlock.BlockHash)
            );
        }
    }
}
