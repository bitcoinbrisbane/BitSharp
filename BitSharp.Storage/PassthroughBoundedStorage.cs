using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public abstract class PassthroughBoundedStorage<TKey, TValue> : IBoundedStorage<TKey, TValue>
    {
        private readonly IBoundedStorage<TKey, TValue> storage;

        public PassthroughBoundedStorage(IBoundedStorage<TKey, TValue> storage)
        {
            this.storage = storage;
        }

        public void Dispose()
        {
            this.storage.Dispose();
        }

        public int Count { get { return this.storage.Count; } }
        public IEnumerable<TKey> Keys { get { return this.storage.Keys; } }
        public IEnumerable<TValue> Values { get { return this.storage.Values; } }

        public bool ContainsKey(TKey key) { return this.storage.ContainsKey(key); }
        public bool TryGetValue(TKey key, out TValue value) { return this.storage.TryGetValue(key, out value); }
        public bool TryAdd(TKey key, TValue value) { return this.storage.TryAdd(key, value); }
        public bool TryRemove(TKey key) { return this.storage.TryRemove(key); }
        public TValue this[TKey key] { get { return this.storage[key]; } set { this.storage[key] = value; } }
        public void Flush() { this.storage.Flush(); }
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() { return this.storage.GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return this.storage.GetEnumerator(); }
    }
}
