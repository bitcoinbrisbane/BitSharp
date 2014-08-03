using BitSharp.Common;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class ChainState : IDisposable
    {
        private readonly IChainStateCursor chainStateCursor;
        private readonly Chain chain;
        private readonly Utxo utxo;

        public ChainState(Chain chain, IChainStateCursor chainStateCursor)
        {
            this.chainStateCursor = chainStateCursor;
            this.chain = chain;
            this.utxo = new Utxo(this.chainStateCursor);
        }

        ~ChainState()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.chainStateCursor.RollbackTransaction();
            this.chainStateCursor.Dispose();
        }

        public Chain Chain { get { return this.chain; } }

        public Utxo Utxo { get { return this.utxo; } }
    }
}
