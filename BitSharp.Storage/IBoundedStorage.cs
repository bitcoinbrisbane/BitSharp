using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBoundedStorage<TKey, TValue> : IUnboundedStorage<TKey, TValue>, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        int Count { get; }

        ICollection<TKey> Keys { get; }

        ICollection<TValue> Values { get; }
    }
}
