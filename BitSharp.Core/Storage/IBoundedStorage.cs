using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IBoundedStorage<TKey, TValue> : IUnboundedStorage<TKey, TValue>, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        int Count { get; }

        IEnumerable<TKey> Keys { get; }

        IEnumerable<TValue> Values { get; }
    }
}
