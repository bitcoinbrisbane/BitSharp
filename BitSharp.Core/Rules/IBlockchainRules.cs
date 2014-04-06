using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Domain.Builders;
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

        ChainedBlock GenesisChainedBlock { get; }

        void ValidateBlock(Block block, ChainStateBuilder chainStateBuilder);
    }
}
