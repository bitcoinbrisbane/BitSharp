using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BoundedCache<TKey, TValue> : UnboundedCache<TKey, TValue>, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        // known keys
        private ConcurrentSet<TKey> knownKeys;

        private readonly IBoundedStorage<TKey, TValue> dataStorage;

        public BoundedCache(string name, IBoundedStorage<TKey, TValue> dataStorage)
            : base(name, dataStorage)
        {
            this.knownKeys = new ConcurrentSet<TKey>();

            this.dataStorage = dataStorage;

            this.OnAddition += (key, value) => AddKnownKey(key);
            this.OnModification += (key, value) => AddKnownKey(key);
            this.OnRetrieved += (key, value) => AddKnownKey(key);
            this.OnMissing += key => RemoveKnownKey(key);

            // load existing keys from storage
            LoadKeysFromStorage();
        }

        public IBoundedStorage<TKey, TValue> DataStorage { get { return this.dataStorage; } }

        // get count of known items
        public int Count
        {
            get { return this.dataStorage.Count; }
        }

        public ICollection<TKey> Keys
        {
            get { return new SimpleCollection<TKey>(() => this.Count, () => this.GetKeysEnumerator()); }
        }

        public ICollection<TValue> Values
        {
            get { return new SimpleCollection<TValue>(() => this.Count, () => this.Select(x => x.Value).GetEnumerator()); }
        }

        public override bool ContainsKey(TKey key)
        {
            return this.knownKeys.Contains(key);
        }

        public override bool TryGetValue(TKey key, out TValue value)
        {
            if (base.TryGetValue(key, out value))
            {
                AddKnownKey(key);
                return true;
            }
            else
            {
                RemoveKnownKey(key);
                return false;
            }
        }

        public override bool TryAdd(TKey key, TValue value)
        {
            var result = base.TryAdd(key, value);
            AddKnownKey(key);

            return result;
        }

        // clear all state and reload
        private void ClearKnownKeys()
        {
            // clear known keys
            this.knownKeys.Clear();

            // reload existing keys from storage
            LoadKeysFromStorage();
        }

        // load all existing keys from storage
        private void LoadKeysFromStorage()
        {
            var count = 0;
            foreach (var key in this.DataStorage.Keys)
            {
                AddKnownKey(key);
                count++;
            }
            Debug.WriteLine("{0}: Finished loading from storage: {1:#,##0}".Format2(this.Name, count));
        }

        // add a key to the known list, fire event if new
        private void AddKnownKey(TKey key)
        {
            // add to the list of known keys
            if (this.knownKeys.TryAdd(key))
                RaiseOnAddition(key, default(TValue));
        }

        // remove a key from the known list, fire event if deleted
        private void RemoveKnownKey(TKey key)
        {
            // remove from the list of known keys
            this.knownKeys.TryRemove(key);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var keyPair in this.dataStorage)
            {
                AddKnownKey(keyPair.Key);
                yield return new KeyValuePair<TKey, TValue>(keyPair.Key, keyPair.Value);
            }
        }

        private IEnumerator<TKey> GetKeysEnumerator()
        {
            foreach (var key in this.dataStorage.Keys)
            {
                AddKnownKey(key);
                yield return key;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
