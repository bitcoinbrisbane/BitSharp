using BitSharp.Common;
using BitSharp.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class ChainState
    {
        private readonly Chain chain;
        private readonly Utxo utxo;

        public ChainState(Chain chain, Utxo utxo)
        {
            this.chain = chain;
            this.utxo = utxo;
        }

        public Chain Chain { get { return this.chain; } }

        public Utxo Utxo { get { return this.utxo; } }

        public ChainedBlock LastBlock { get { return this.chain.LastBlock; } }

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
                Chain.CreateForGenesisBlock(genesisBlock),
                Utxo.CreateForGenesisBlock(genesisBlock.BlockHash)
            );
        }
    }
}
