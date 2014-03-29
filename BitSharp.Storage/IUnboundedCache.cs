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
    public interface IUnboundedCache<TKey, TValue> : IDisposable
    {
        event Action<TKey> OnMissing;

        string Name { get; }

        ImmutableHashSet<TKey> MissingData { get; }

        bool ContainsKey(TKey key);

        bool TryGetValue(TKey key, out TValue value);

        bool TryAdd(TKey key, TValue value);

        bool TryRemove(TKey key);

        TValue this[TKey key] { get; set; }

        void Flush();
    }
}
