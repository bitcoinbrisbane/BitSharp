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
    public class ChainState
    {
        private readonly ChainedBlocks chainedBlocks;
        private readonly Utxo utxo;

        public ChainState(ChainedBlocks chainedBlocks, Utxo utxo)
        {
            this.chainedBlocks = chainedBlocks;
            this.utxo = utxo;
        }

        public ChainedBlocks ChainedBlocks { get { return this.chainedBlocks; } }

        public Utxo Utxo { get { return this.utxo; } }

        public ChainedBlock LastBlock { get { return this.chainedBlocks.LastBlock; } }

        public UInt256 LastBlockHash
        {
            get
            {
                var lastBlockLocal = this.LastBlock;
                if (lastBlockLocal != null)
                    return this.LastBlock.BlockHash;
                else
                    return UInt256.Zero;
            }
        }

        public int Height
        {
            get
            {
                var lastBlockLocal = this.LastBlock;
                if (lastBlockLocal != null)
                    return this.LastBlock.Height;
                else
                    return -1;
            }
        }

        public static ChainState CreateForGenesisBlock(ChainedBlock genesisBlock)
        {
            return new ChainState(
                ChainedBlocks.CreateForGenesisBlock(genesisBlock),
                Utxo.CreateForGenesisBlock(genesisBlock.BlockHash)
            );
        }
    }
}
