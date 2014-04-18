using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Builders;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Rules
{
    public interface IBlockchainRules
    {
        UInt256 HighestTarget { get; }

        Block GenesisBlock { get; }

        ChainedHeader GenesisChainedHeader { get; }

        void ValidateBlock(Block block, ChainStateBuilder chainStateBuilder);

        void ValidationTransactionScript(Block block, Transaction tx, int txIndex, TxInput txInput, int txInputIndex, TxOutput prevTxOutput);
    }
}
