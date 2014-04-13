using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Wallet
{
    public class MonitoredWalletAddress
    {
        private readonly WalletAddress address;
        private readonly List<Tuple<ChainPosition, ChainPosition>> monitoredRanges;

        public MonitoredWalletAddress(WalletAddress address, List<Tuple<ChainPosition, ChainPosition>> monitoredRanges)
        {
            this.address = address;
            this.monitoredRanges = monitoredRanges;
        }

        // address
        public WalletAddress Address { get { return this.address; } }

        // ranges in the blockchain when monitoring was active
        public List<Tuple<ChainPosition, ChainPosition>> MonitoredRanges { get { return this.monitoredRanges; } }
    }
}
