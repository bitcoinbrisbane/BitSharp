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
        void AddBlock(Block block);

        bool TryGetTransaction(UInt256 blockHash, int txIndex, out Transaction transaction);

        IEnumerable<BlockTx> ReadBlock(UInt256 blockHash, UInt256 merkleRoot);

        IEnumerable<BlockElement> ReadBlockElements(UInt256 blockHash, UInt256 merkleRoot);





        event Action<UInt256, Block> OnAddition;
        event Action<UInt256, Block> OnModification;
        event Action<UInt256> OnRemoved;

        int Count { get; }

        IEnumerable<UInt256> Keys { get; }

        IEnumerable<Block> Values { get; }





        event Action<UInt256> OnMissing;

        string Name { get; }

        ImmutableHashSet<UInt256> MissingData { get; }

        bool ContainsKey(UInt256 blockHash);

        bool TryGetValue(UInt256 blockHash, out Block block);

        bool TryAdd(UInt256 blockHash, Block block);

        bool TryRemove(UInt256 blockHash);

        Block this[UInt256 blockHash] { get; set; }

        void Flush();
    }
}
