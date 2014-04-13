using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Wallet
{
    public class WalletAddress
    {
        // point in the blockchain when monitoring started
        private readonly ChainPosition startChainPosition;

        // address, in this case a single sha-256 of the output script
        private readonly UInt256 outputScriptHash;

        public WalletAddress(UInt256 outputScriptHash)
        {
            this.outputScriptHash = outputScriptHash;
        }

        public IEnumerable<UInt256> GetOutputScriptHashes()
        {
            yield return outputScriptHash;
        }

        // determine if a tx output matches this wallet address
        public bool MatchesTxOutput(TxOutput txOutput, UInt256 txOutputScriptHash)
        {
            return this.outputScriptHash == txOutputScriptHash;
        }
    }
}
