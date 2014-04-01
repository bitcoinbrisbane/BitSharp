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

    public sealed class MemoryBlockHeaderStorage : MemoryStorage<UInt256, BlockHeader>, IBlockHeaderStorage { }

    public sealed class MemoryChainedBlockStorage : MemoryStorage<UInt256, ChainedBlock>, IChainedBlockStorage { }

    public sealed class MemoryBlockTxHashesStorage : MemoryStorage<UInt256, IImmutableList<UInt256>>, IBlockTxHashesStorage { }

    public sealed class MemoryTransactionStorage : MemoryStorage<UInt256, Transaction>, ITransactionStorage { }

    public sealed class MemoryBlockRollbackStorage : MemoryStorage<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>>, IBlockRollbackStorage { }

    public sealed class MemorySpentOutputsStorage : MemoryStorage<UInt256, IImmutableList<KeyValuePair<TxOutputKey, TxOutput>>>, ISpentOutputsStorage { }

    public sealed class MemoryInvalidBlockStorage : MemoryStorage<UInt256, string>, IInvalidBlockStorage { }

    public sealed class MemoryNetworkPeerStorage : MemoryStorage<NetworkAddressKey, NetworkAddressWithTime>, INetworkPeerStorage { }
}
