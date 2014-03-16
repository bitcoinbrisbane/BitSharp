using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class UnboundedCache<TKey, TValue>
    {
        public event Action<TKey, TValue> OnAddition;
        public event Action<TKey, TValue> OnModification;
        public event Action<TKey, TValue> OnRetrieved;
        public event Action<TKey> OnMissing;

        private readonly string name;
        private readonly IUnboundedStorage<TKey, TValue> dataStorage;

        private readonly ConcurrentSetBuilder<TKey> missingData;

        public UnboundedCache(string name, IUnboundedStorage<TKey, TValue> dataStorage)
        {
            this.name = name;
            this.dataStorage = dataStorage;
            this.missingData = new ConcurrentSetBuilder<TKey>();
        }

        public string Name { get { return this.name; } }

        public ImmutableHashSet<TKey> MissingData { get { return this.missingData.ToImmutable(); } }

        public virtual bool ContainsKey(TKey key)
        {
            return this.dataStorage.ContainsKey(key);
        }

        // try to get a value
        public virtual bool TryGetValue(TKey key, out TValue value)
        {
            // look in storage
            if (this.dataStorage.TryGetValue(key, out value))
            {
                this.missingData.Remove(key);

                // value found, fire retrieved event
                var handler = this.OnRetrieved;
                if (handler != null)
                    handler(key, value);

                return true;
            }
            else
            {
                // no value found, fire missing event
                var handler = this.OnMissing;
                if (handler != null)
                    handler(key);

                return false;
            }
        }

        public virtual bool TryAdd(TKey key, TValue value)
        {
            var result = this.dataStorage.TryAdd(key, value);

            this.missingData.Remove(key);

            var handler = this.OnAddition;
            if (handler != null)
                handler(key, value);

            return result;
        }

        public virtual TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (this.TryGetValue(key, out value))
                    return value;
                else
                {
                    this.missingData.Add(key);

                    throw new MissingDataException(key);
                }
            }
            set
            {
                this.dataStorage[key] = value;

                this.missingData.Remove(key);

                var handler = this.OnModification;
                if (handler != null)
                    handler(key, value);
            }
        }

        protected void RaiseOnAddition(TKey key, TValue value)
        {
            var handler = this.OnAddition;
            if (handler != null)
                handler(key, value);
        }
    }
}
