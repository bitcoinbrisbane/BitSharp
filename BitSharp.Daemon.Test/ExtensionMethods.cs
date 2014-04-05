using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Daemon.Test
{
    public static class ExtensionMethods
    {
        public static Block WithAddedTransactions(this Block block, params Transaction[] transactions)
        {
            // update transactions
            block = block.With(Transactions: block.Transactions.AddRange(transactions));

            // update merkle root
            block = block.With(block.Header.With(MerkleRoot: DataCalculator.CalculateMerkleRoot(block.Transactions.Select(x => x.Hash).ToImmutableList())));

            return block;
        }
    }
}
