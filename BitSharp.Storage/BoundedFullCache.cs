using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BoundedFullCache<TKey, TValue> : IBoundedCache<TKey, TValue>
    {
        public event Action<TKey, TValue> OnAddition;
        public event Action<TKey, TValue> OnModification;
        public event Action<TKey> OnRemoved;
        public event Action<TKey> OnMissing;

        private readonly string name;
        private readonly IBoundedStorage<TKey, TValue> dataStorage;
        private readonly ConcurrentDictionary<TKey, TValue> cache;
        private readonly ConcurrentSetBuilder<TKey> missingData;

        public BoundedFullCache(string name, IBoundedStorage<TKey, TValue> dataStorage)
        {
            this.name = name;
            this.dataStorage = dataStorage;
            this.missingData = new ConcurrentSetBuilder<TKey>();

            // load from storage
            this.cache = new ConcurrentDictionary<TKey, TValue>(this.dataStorage);
            Debug.WriteLine("{0}: Finished loading from storage: {1:#,##0}".Format2(this.Name, this.Count));
        }

        public string Name { get { return this.name; } }

        public ImmutableHashSet<TKey> MissingData { get { return this.missingData.ToImmutable(); } }

        public int Count
        {
            get { return this.cache.Count; }
        }

        public IEnumerable<TKey> Keys
        {
            get { return this.cache.Keys.Select(x => x); }
        }

        public IEnumerable<TValue> Values
        {
            get { return this.cache.Values.Select(x => x); }
        }

        public bool ContainsKey(TKey key)
        {
            return this.cache.ContainsKey(key);
        }

        // try to get a value
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (this.cache.TryGetValue(key, out value))
            {
                this.missingData.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryAdd(TKey key, TValue value)
        {
            this.missingData.Remove(key);
            if (this.cache.TryAdd(key, value))
            {
                RaiseOnAddition(key, value);
                this.dataStorage.TryAdd(key, value);

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryRemove(TKey key)
        {
            this.missingData.Remove(key);

            TValue ignore;
            if (this.cache.TryRemove(key, out ignore))
            {
                RaiseOnRemoved(key);
                this.dataStorage.TryRemove(key);

                return true;
            }
            else
            {
                return false;
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (this.TryGetValue(key, out value))
                {
                    return value;
                }
                else
                {
                    this.missingData.Add(key);
                    throw new MissingDataException(key);
                }
            }
            set
            {
                var wasPresent = this.cache.ContainsKey(key);
                this.cache[key] = value;
                this.dataStorage[key] = value;
                this.missingData.Remove(key);

                if (!wasPresent)
                    RaiseOnAddition(key, value);
                else
                    RaiseOnModification(key, value);
            }
        }

        public void Flush()
        {
            this.dataStorage.Flush();
        }

        private void RaiseOnAddition(TKey key, TValue value)
        {
            var handler = this.OnAddition;
            if (handler != null)
                handler(key, value);
        }

        private void RaiseOnModification(TKey key, TValue value)
        {
            var handler = this.OnModification;
            if (handler != null)
                handler(key, value);
        }

        private void RaiseOnRemoved(TKey key)
        {
            var handler = this.OnRemoved;
            if (handler != null)
                handler(key);
        }

        private void RaiseOnMissing(TKey key)
        {
            var handler = this.OnMissing;
            if (handler != null)
                handler(key);
        }
    }
}
