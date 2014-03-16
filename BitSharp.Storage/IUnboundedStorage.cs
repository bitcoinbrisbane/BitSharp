using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IUnboundedStorage<TKey, TValue> : IDisposable
    {
        bool TryReadValue(TKey key, out TValue value);

        bool TryWriteValues(IEnumerable<KeyValuePair<TKey, WriteValue<TValue>>> keyPairs);
    }

    public static class IUnboundedStorageExtensionMethods
    {
        public static bool TryWriteValue<TKey, TValue>(this IUnboundedStorage<TKey, TValue> storage, TKey key, TValue value, bool isCreate)
        {
            return storage.TryWriteValues(new KeyValuePair<TKey, WriteValue<TValue>>[] { new KeyValuePair<TKey, WriteValue<TValue>>(key, new WriteValue<TValue>(value, isCreate)) });
        }

        public static bool TryCreateValue<TKey, TValue>(this IUnboundedStorage<TKey, TValue> storage, TKey key, TValue value)
        {
            return storage.TryWriteValue(key, value, isCreate: true);
        }

        public static bool TryUpdateValue<TKey, TValue>(this IUnboundedStorage<TKey, TValue> storage, TKey key, TValue value)
        {
            return storage.TryWriteValue(key, value, isCreate: false);
        }
    }
}
