using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IStorageContext : IDisposable
    {
        IBoundedStorage<UInt256, BlockHeader> BlockHeaderStorage { get; }

        IBoundedStorage<UInt256, ChainedBlock> ChainedBlockStorage { get; }

        IBoundedStorage<UInt256, IImmutableList<UInt256>> BlockTxHashesStorage { get; }

        IUnboundedStorage<UInt256, Transaction> TransactionStorage { get; }

        IEnumerable<ChainedBlock> SelectMaxTotalWorkBlocks();
        
        IUtxoBuilderStorage ToUtxoBuilder(Utxo utxo);
    }
}
