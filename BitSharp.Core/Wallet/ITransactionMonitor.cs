using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Wallet
{
    public interface ITransactionMonitor
    {
        void MintTxOutput(ChainPosition chainPosition, TxOutput txOutput, UInt256 outputScriptHash);
        void ReceiveTxOutput(ChainPosition chainPosition, TxOutput txOutput, UInt256 outputScriptHash);
        void SpendTxOutput(ChainPosition chainPosition, TxOutput txOutput, UInt256 outputScriptHash);
    }
}
