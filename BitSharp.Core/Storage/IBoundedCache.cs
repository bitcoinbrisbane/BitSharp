using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
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

namespace BitSharp.Core.Storage
{
    public interface IBoundedCache<TKey, TValue> : IUnboundedCache<TKey, TValue>
    {
        event Action<TKey, TValue> OnAddition;
        event Action<TKey, TValue> OnModification;
        event Action<TKey> OnRemoved;

        int Count { get; }

        IEnumerable<TKey> Keys { get; }

        IEnumerable<TValue> Values { get; }
    }
}
