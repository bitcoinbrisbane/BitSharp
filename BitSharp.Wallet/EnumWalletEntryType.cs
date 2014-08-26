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
        Spend,

        UnMine,
        UnReceieve,
        UnSpend
        //TODO DoubleSpend, MutatedReceive, MutatedSpend
    }

    public static class WalletEntryTypeExtensionMethods
    {
        public static int Direction(this EnumWalletEntryType value)
        {
            switch (value)
            {
                case EnumWalletEntryType.Receive:
                case EnumWalletEntryType.Mine:
                case EnumWalletEntryType.UnSpend:
                    return +1;

                default:
                    return -1;
            }
        }
    }
}
