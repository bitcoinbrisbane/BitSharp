using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Wallet
{
    public enum EnumWalletEntryType
    {
        Mint,
        Receive,
        Spend
        //TODO Unmint, Unspend, DoubleSpend
    }
}
