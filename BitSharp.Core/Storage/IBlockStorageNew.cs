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
    public interface IBlockStorageNew : IDisposable
    {
        //TODO remove events
        event Action<UInt256, Block> OnAddition;
        event Action<UInt256, Block> OnModification;
        event Action<UInt256> OnRemoved;
        event Action<UInt256> OnMissing;

        void AddBlock(Block block);

        bool TryGetTransaction(UInt256 blockHash, int txIndex, out Transaction transaction);

        //TODO merkle check shouldn't be at this level
        IEnumerable<BlockTx> ReadBlock(UInt256 blockHash, UInt256 merkleRoot);

        //TODO merkle check shouldn't be at this level
        IEnumerable<BlockElement> ReadBlockElements(UInt256 blockHash, UInt256 merkleRoot);

        void PruneElements(UInt256 blockHash, IEnumerable<int> indices);

        int Count { get; }

        string Name { get; }

        ImmutableHashSet<UInt256> MissingData { get; }

        bool ContainsKey(UInt256 blockHash);

        bool TryGetValue(UInt256 blockHash, out Block block);

        bool TryAdd(UInt256 blockHash, Block block);

        bool TryRemove(UInt256 blockHash);

        void Flush();
    }
}
