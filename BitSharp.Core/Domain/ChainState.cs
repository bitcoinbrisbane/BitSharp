using BitSharp.Common;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
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
        private readonly IChainStateStorage chainStateStorage;
        private readonly Chain chain;
        private readonly Utxo utxo;

        public ChainState(Chain chain, IChainStateCursor chainStateCursor)
        {
            this.chainStateStorage = chainStateCursor.ToImmutable();
            this.chain = chain;
            this.utxo = new Utxo(this.chainStateStorage);
        }

        ~ChainState()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.chainStateStorage.Dispose();
        }

        public Chain Chain { get { return this.chain; } }

        public Utxo Utxo { get { return this.utxo; } }
    }
}
