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
    public class ChainState : IDisposable
    {
        private readonly Chain chain;
        private readonly Utxo utxo;

        public ChainState(Chain chain, Utxo utxo)
        {
            this.chain = chain;
            this.utxo = utxo;
        }

        ~ChainState()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.utxo.Dispose();
        }

        public Chain Chain { get { return this.chain; } }

        public Utxo Utxo { get { return this.utxo; } }

        public ChainedHeader LastBlock { get { return this.chain.LastBlock; } }

        public UInt256 LastBlockHash
        {
            get
            {
                var lastBlockLocal = this.LastBlock;
                if (lastBlockLocal != null)
                    return this.LastBlock.Hash;
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

        public static ChainState CreateForGenesisBlock(ChainedHeader genesisBlock)
        {
            return new ChainState(
                Chain.CreateForGenesisBlock(genesisBlock),
                Utxo.CreateForGenesisBlock(genesisBlock.Hash)
            );
        }
    }
}
