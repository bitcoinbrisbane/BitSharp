using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class MemoryStorage<TKey, TValue> : IBoundedStorage<TKey, TValue>
    {
        private readonly MemoryStorageContext storageContext;
        private readonly ConcurrentDictionary<TKey, TValue> storage = new ConcurrentDictionary<TKey, TValue>();

        public MemoryStorage(MemoryStorageContext storageContext)
        {
            this.storageContext = storageContext;
        }

        public MemoryStorageContext StorageContext { get { return this.storageContext; } }

        internal ConcurrentDictionary<TKey, TValue> Storage { get { return this.storage; } }

        public void Dispose()
        {
        }

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

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return this.storage.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
