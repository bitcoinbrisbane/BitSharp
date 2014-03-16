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
        private readonly ChainedBlocks currentChainedBlocks;
        private readonly Utxo currentUtxo;

        public ChainState(ChainedBlocks currentChainedBlocks, Utxo currentUtxo)
        {
            this.currentChainedBlocks = currentChainedBlocks;
            this.currentUtxo = currentUtxo;
        }

        public ChainedBlocks CurrentChainedBlocks { get { return this.currentChainedBlocks; } }

        public Utxo CurrentUtxo { get { return this.currentUtxo; } }

        public ChainedBlock CurrentBlock { get { return this.currentChainedBlocks.LastBlock; } }

        public ChainStateBuilder ToBuilder(Func<Utxo, UtxoBuilder> utxoBuilderFactory)
        {
            return new ChainStateBuilder(this.currentChainedBlocks.ToBuilder(), utxoBuilderFactory(this.currentUtxo));
        }

        public static ChainState CreateForGenesisBlock(ChainedBlock genesisBlock, Func<Utxo, UtxoBuilder> utxoBuilderFactory)
        {
            return new ChainState(
                ChainedBlocks.CreateForGenesisBlock(genesisBlock),
                new GenesisUtxo(genesisBlock.BlockHash, utxoBuilderFactory)
            );
        }
    }
}
