using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class TransactionCache : ITransactionStorage
    {
        private readonly CacheContext cacheContext;
        private readonly ITransactionStorage txStorage;
        private readonly ConcurrentSetBuilder<UInt256> missingData;

        public TransactionCache(CacheContext cacheContext)
        {
            this.cacheContext = cacheContext;
            this.txStorage = cacheContext.StorageContext.TransactionStorage;
            this.missingData = new ConcurrentSetBuilder<UInt256>();
        }

        public CacheContext CacheContext { get { return this.cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public ImmutableHashSet<UInt256> MissingData { get { return this.missingData.ToImmutable(); } }

        public bool ContainsKey(UInt256 key)
        {
            return this.txStorage.ContainsKey(key);
        }

        public bool TryGetValue(UInt256 key, out Transaction value)
        {
            if (this.txStorage.TryGetValue(key, out value))
            {
                this.missingData.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryAdd(UInt256 key, Transaction value)
        {
            var result = this.txStorage.TryAdd(key, value);
            this.missingData.Remove(key);
            return result;
        }

        public Transaction this[UInt256 key]
        {
            get
            {
                Transaction value;
                if (this.txStorage.TryGetValue(key, out value))
                {
                    this.missingData.Remove(key);
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
                this.txStorage[key] = value;
                this.missingData.Remove(key);
            }
        }

        public void Dispose()
        {
            this.txStorage.Dispose();
        }
    }
}
