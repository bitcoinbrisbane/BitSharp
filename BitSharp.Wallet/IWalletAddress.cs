using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Wallet
{
    public interface IWalletAddress
    {
        IEnumerable<UInt256> GetOutputScriptHashes();

        // determine if a tx output matches this wallet address
        bool IsMatcher { get; }
        bool MatchesTxOutput(TxOutput txOutput, UInt256 txOutputScriptHash);
    }
}
