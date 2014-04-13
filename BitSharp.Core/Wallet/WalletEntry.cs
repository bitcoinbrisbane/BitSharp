using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Wallet
{
    public class WalletEntry
    {
        private readonly WalletAddress address;
        private readonly EnumWalletEntryType type;
        private readonly UInt256 blockHash;
        private readonly UInt256 txHash;
        private readonly int inputIndex;
        private readonly int outputIndex;
        private readonly UInt64 value;
    }
}
