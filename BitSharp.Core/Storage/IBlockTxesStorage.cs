using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IBlockTxesStorage : IDisposable
    {
        bool TryGetTransaction(UInt256 blockHash, int txIndex, out Transaction transaction);

        IEnumerable<BlockTx> ReadBlockTransactions(UInt256 blockHash);

        IEnumerable<BlockElement> ReadBlockElements(UInt256 blockHash);

        void PruneElements(UInt256 blockHash, IEnumerable<int> indices);

        int Count { get; }

        string Name { get; }

        bool ContainsBlock(UInt256 blockHash);

        bool TryAdd(UInt256 blockHash, Block block);

        bool TryRemove(UInt256 blockHash);

        void Flush();
    }
}
