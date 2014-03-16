using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.Data
{
    public class Block
    {
        private readonly BlockHeader _header;
        private readonly ImmutableList<Transaction> _transactions;

        public Block(BlockHeader header, ImmutableList<Transaction> transactions)
        {
            this._header = header;
            this._transactions = transactions;
        }

        public UInt256 Hash { get { return this.Header.Hash; } }

        public BlockHeader Header { get { return this._header; } }

        public ImmutableList<Transaction> Transactions { get { return this._transactions; } }

        public Block With(BlockHeader Header = null, ImmutableList<Transaction> Transactions = null)
        {
            return new Block
            (
                Header ?? this.Header,
                Transactions ?? this.Transactions
            );
        }

        //TODO for unit test, move elsewhere
        public Block WithAddedTransactions(params Transaction[] transactions)
        {
            return this.With(Transactions: this.Transactions.AddRange(transactions));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Block))
                return false;

            return (Block)obj == this;
        }

        public static bool operator ==(Block left, Block right)
        {
            return object.ReferenceEquals(left, right) || (!object.ReferenceEquals(left, null) && !object.ReferenceEquals(right, null) && left.Header == right.Header && left.Transactions.SequenceEqual(right.Transactions));
        }

        public static bool operator !=(Block left, Block right)
        {
            return !(left == right);
        }
    }
}
