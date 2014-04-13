using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Wallet
{
    public class Wallet : ITransactionMonitor
    {
        // addresses
        private readonly List<WalletAddress> addresses;

        // current point in the blockchain

        // entries
        private readonly List<WalletEntry> entries;

        public void MintTxOutput(Domain.TxOutput txOutput)
        {
            throw new NotImplementedException();
        }

        public void SpendTxOutput(Domain.TxOutput txOutput)
        {
            throw new NotImplementedException();
        }
    }
}
