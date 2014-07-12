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

        //TODO
        //void ValidateBlock(ChainedBlock chainedBlock, ChainStateBuilder chainStateBuilder);

        void ValidateTransaction(ChainedHeader chainedHeader, Transaction tx, int txIndex, ImmutableArray<TxOutput> prevTxOutputs);

        void ValidationTransactionScript(ChainedHeader chainedHeader, Transaction tx, int txIndex, TxInput txInput, int txInputIndex, TxOutput prevTxOutput);
    }
}
