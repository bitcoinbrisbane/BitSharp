using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Wallet
{
    public enum EnumWalletEntryType
    {
        Mine,
        Receive,
        Spend
        //TODO Unmine, Unspend, DoubleSpend, MutatedReceive, MutatedSpend
    }
}
