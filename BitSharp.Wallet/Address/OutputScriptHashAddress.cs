using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Wallet.Address
{
    public class OutputScriptHashAddress : IWalletAddress
    {
        // address, in this case a single sha-256 of the output script
        private readonly UInt256 outputScriptHash;

        public OutputScriptHashAddress(UInt256 outputScriptHash)
        {
            this.outputScriptHash = outputScriptHash;
        }

        public IEnumerable<UInt256> GetOutputScriptHashes()
        {
            yield return outputScriptHash;
        }

        public bool IsMatcher { get { return false; } }

        public bool MatchesTxOutput(TxOutput txOutput, UInt256 txOutputScriptHash)
        {
            throw new NotSupportedException();
        }
    }
}
