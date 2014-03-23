using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface ICacheContext
    {
        IBoundedCache<UInt256, BlockHeader> BlockHeaderCache { get; }

        IBoundedCache<UInt256, ChainedBlock> ChainedBlockCache { get; }

        IBoundedCache<UInt256, IImmutableList<UInt256>> BlockTxHashesCache { get; }

        IUnboundedCache<UInt256, Transaction> TransactionCache { get; }

        IBoundedCache<UInt256, string> InvalidBlockCache { get; }

        IUnboundedCache<UInt256, Block> BlockView { get; }

        //TODO
        IUtxoBuilderStorage ToUtxoBuilder(Utxo utxo);

        //TODO
        //IEnumerable<ChainedBlock> SelectMaxTotalWorkBlocks();
    }
}
