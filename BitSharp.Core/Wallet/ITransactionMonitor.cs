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
        void MintTxOutput(TxOutput txOutput);
        void SpendTxOutput(TxOutput txOutput);
    }
}
