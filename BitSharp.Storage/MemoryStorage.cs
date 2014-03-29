using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Network;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class MemoryStorage<TKey, TValue> : IBoundedStorage<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> storage = new ConcurrentDictionary<TKey, TValue>();

        public void Dispose()
        {
        }

        internal ConcurrentDictionary<TKey, TValue> Storage { get { return this.storage; } }

        public int Count
        {
            get { return this.storage.Count; }
        }

        public IEnumerable<TKey> Keys
        {
            get { return this.storage.Keys; }
        }

        public IEnumerable<TValue> Values
        {
            get { return this.storage.Values; }
        }

        public bool ContainsKey(TKey key)
        {
            return this.storage.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return this.storage.TryGetValue(key, out value);
        }

        public bool TryAdd(TKey key, TValue value)
        {
            return this.storage.TryAdd(key, value);
        }

        public bool TryRemove(TKey key)
        {
            TValue ignore;
            return this.storage.TryRemove(key, out ignore);
        }

        public TValue this[TKey key]
        {
            get
            {
                return this.storage[key];
            }
            set
            {
                this.storage[key] = value;
            }
        }

        public void Flush()
        {
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return this.storage.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    public sealed class MemoryBlockHeaderStorage : PassthroughBoundedStorage<UInt256, BlockHeader>, IBlockHeaderStorage
    {
        public MemoryBlockHeaderStorage(IBoundedStorage<UInt256, BlockHeader> storage)
            : base(storage) { }
    }

    public sealed class MemoryChainedBlockStorage : PassthroughBoundedStorage<UInt256, ChainedBlock>, IChainedBlockStorage
    {
        public MemoryChainedBlockStorage(IBoundedStorage<UInt256, ChainedBlock> storage)
            : base(storage) { }
    }

    public sealed class MemoryBlockTxHashesStorage : PassthroughBoundedStorage<UInt256, IImmutableList<UInt256>>, IBlockTxHashesStorage
    {
        public MemoryBlockTxHashesStorage(IBoundedStorage<UInt256, IImmutableList<UInt256>> storage)
            : base(storage) { }
    }

    public sealed class MemoryTransactionStorage : PassthroughBoundedStorage<UInt256, Transaction>, ITransactionStorage
    {
        public MemoryTransactionStorage(IBoundedStorage<UInt256, Transaction> storage)
            : base(storage) { }
    }

    public sealed class MemoryBlockRollbackStorage : PassthroughBoundedStorage<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>>, IBlockRollbackStorage
    {
        public MemoryBlockRollbackStorage(IBoundedStorage<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>> storage)
            : base(storage) { }
    }

    public sealed class MemoryInvalidBlockStorage : PassthroughBoundedStorage<UInt256, string>, IInvalidBlockStorage
    {
        public MemoryInvalidBlockStorage(IBoundedStorage<UInt256, string> storage)
            : base(storage) { }
    }

    public sealed class MemoryNetworkPeerStorage : PassthroughBoundedStorage<NetworkAddressKey, NetworkAddressWithTime>, INetworkPeerStorage
    {
        public MemoryNetworkPeerStorage(IBoundedStorage<NetworkAddressKey, NetworkAddressWithTime> storage)
            : base(storage) { }
    }
}
