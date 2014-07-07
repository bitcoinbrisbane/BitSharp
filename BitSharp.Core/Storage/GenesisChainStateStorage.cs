using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public class GenesisChainStateStorage : IChainStateStorage
    {
        // genesis block coinbase is not included in utxo, it is unspendable

        private readonly Chain genesisChain;

        public GenesisChainStateStorage(ChainedHeader genesisHeader)
        {
            this.genesisChain = Chain.CreateForGenesisBlock(genesisHeader);
        }

        public void Dispose()
        {
        }

        public Chain Chain
        {
            get { return this.genesisChain; }
        }

        public int TransactionCount
        {
            get { return 0; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return false;
        }

        public bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx)
        {
            unspentTx = default(UnspentTx);
            return false;
        }

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> ReadUnspentTransactions()
        {
            return Enumerable.Empty<KeyValuePair<UInt256, UnspentTx>>();
        }

        public IChainStateBuilderStorage ToBuilder()
        {
            throw new NotSupportedException();
        }
    }
}
