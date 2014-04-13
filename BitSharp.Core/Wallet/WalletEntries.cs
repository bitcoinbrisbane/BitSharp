using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Wallet
{
    public class WalletEntries
    {
        // point in the blockchain when monitoring started
        private readonly UInt256 startBlockHash;
        private readonly int startBlockHeight;
        private readonly int startTxIndex;
        private readonly int startInputIndex;
        private readonly int startOutputIndex;

        // current point in the blockchain

        // entries
        private readonly List<WalletEntry> entries;
    }
}
