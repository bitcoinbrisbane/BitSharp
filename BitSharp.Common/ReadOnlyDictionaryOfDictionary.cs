using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class ReadOnlyDictionaryOfDictionary<TOuterKey, TInnerKey, TInnerValue> : IReadOnlyDictionary<TOuterKey, IReadOnlyDictionary<TInnerKey, TInnerValue>>
    {
        private readonly Dictionary<TOuterKey, Dictionary<TInnerKey, TInnerValue>> outerDictionary;

        public ReadOnlyDictionaryOfDictionary(Dictionary<TOuterKey, Dictionary<TInnerKey, TInnerValue>> dictionary)
        {
            this.outerDictionary = dictionary;
        }

        public bool ContainsKey(TOuterKey key)
        {
            return this.outerDictionary.ContainsKey(key);
        }

        public IEnumerable<TOuterKey> Keys
        {
            get { return this.outerDictionary.Keys; }
        }

        public bool TryGetValue(TOuterKey key, out IReadOnlyDictionary<TInnerKey, TInnerValue> value)
        {
            Dictionary<TInnerKey, TInnerValue> innerDictionary;
            if (this.outerDictionary.TryGetValue(key, out innerDictionary))
            {
                value = new ReadOnlyDictionary<TInnerKey, TInnerValue>(innerDictionary);
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerable<IReadOnlyDictionary<TInnerKey, TInnerValue>> Values
        {
            get
            {
                foreach (var innerDictionary in this.outerDictionary.Values)
                {
                    yield return new ReadOnlyDictionary<TInnerKey, TInnerValue>(innerDictionary);
                }
            }
        }

        public IReadOnlyDictionary<TInnerKey, TInnerValue> this[TOuterKey key]
        {
            get
            {
                var innerDictionary = this.outerDictionary[key];
                if (innerDictionary != null)
                    return new ReadOnlyDictionary<TInnerKey, TInnerValue>(innerDictionary);
                else
                    return null;
            }
        }

        public int Count
        {
            get { return this.outerDictionary.Count; }
        }

        public IEnumerator<KeyValuePair<TOuterKey, IReadOnlyDictionary<TInnerKey, TInnerValue>>> GetEnumerator()
        {
            foreach (var keyPair in this.outerDictionary)
            {
                yield return new KeyValuePair<TOuterKey, IReadOnlyDictionary<TInnerKey, TInnerValue>>(
                    keyPair.Key, new ReadOnlyDictionary<TInnerKey, TInnerValue>(keyPair.Value));
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
