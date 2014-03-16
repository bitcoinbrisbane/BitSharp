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
    public class UnboundedCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        public event Action<TKey, TValue> OnAddition;
        public event Action<TKey, TValue> OnModification;
        public event Action<TKey, TValue> OnRetrieved;
        public event Action<TKey> OnMissing;

        private readonly string _name;

        private readonly IUnboundedStorage<TKey, TValue> dataStorage;

        public UnboundedCache(string name, IUnboundedStorage<TKey, TValue> dataStorage)
        {
            this._name = name;

            this.dataStorage = dataStorage;
        }

        public string Name { get { return this._name; } }

        // try to get a value
        public virtual bool TryGetValue(TKey key, out TValue value)
        {
            // look in storage
            if (TryGetStorageValue(key, out value))
                return true;

            // no value found in storage, fire missing event
            var handler = this.OnMissing;
            if (handler != null)
                handler(key);

            value = default(TValue);
            return false;
        }

        public virtual void CreateValue(TKey key, TValue value)
        {
            if (!this.dataStorage.TryCreateValue(key, value))
                throw new Exception("TODO");

            var handler = this.OnAddition;
            if (handler != null)
                handler(key, value);
        }

        public virtual void UpdateValue(TKey key, TValue value)
        {
            if (!this.dataStorage.TryUpdateValue(key, value))
                throw new Exception("TODO");

            var handler = this.OnModification;
            if (handler != null)
                handler(key, value);
        }

        protected bool TryGetStorageValue(TKey key, out TValue value)
        {
            TValue valueLocal = default(TValue);
            var result = new MethodTimer(false && this.Name == "BlockCache").Time(() =>
            {
                TValue storedValue;
                if (dataStorage.TryReadValue(key, out storedValue))
                {
                    // fire retrieved event
                    var handler = this.OnRetrieved;
                    if (handler != null)
                        handler(key, storedValue);

                    valueLocal = storedValue;
                    return true;
                }
                else
                {
                    valueLocal = default(TValue);
                    return false;
                }
            });

            value = valueLocal;
            return result;
        }

        protected void RaiseOnAddition(TKey key, TValue value)
        {
            var handler = this.OnAddition;
            if (handler != null)
                handler(key, value);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
