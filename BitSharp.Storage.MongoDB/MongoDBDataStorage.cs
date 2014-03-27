using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using System.IO;
using System.Reflection;
using System.Data.Common;
using BitSharp.Storage;
using System.Threading;
using System.Diagnostics;
using BitSharp.Common;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace BitSharp.Storage.MongoDB
{
    public abstract class MongoDBDataStorage<TValue> : IBoundedStorage<UInt256, TValue>
    {
        private readonly MongoDBStorageContext storageContext;
        private readonly string name;
        private readonly string directory;
        private readonly MongoByteCollection collection;

        private readonly Func<TValue, byte[]> encoder;
        private readonly Func<UInt256, byte[], TValue> decoder;

        public MongoDBDataStorage(MongoDBStorageContext storageContext, string name, Func<TValue, byte[]> encoder, Func<UInt256, byte[], TValue> decoder)
        {
            this.storageContext = storageContext;
            this.name = name;
            this.directory = Path.Combine(storageContext.BaseDirectory, name);
            this.collection = new MongoByteCollection(storageContext.Database, name);

            this.encoder = encoder;
            this.decoder = decoder;
        }

        public MongoDBStorageContext StorageContext { get { return this.storageContext; } }

        public void Dispose()
        {
            //this.collection.Dispose();
        }

        internal MongoByteCollection Data { get { return this.collection; } }

        public int Count
        {
            get { return this.collection.Count; }
        }

        public IEnumerable<UInt256> Keys
        {
            get { return this.collection.Keys.Select(x => new UInt256(x)); }
        }

        public IEnumerable<TValue> Values
        {
            get { return this.collection.Select(x => this.decoder(new UInt256(x.Key), x.Value)); }
        }

        public bool ContainsKey(UInt256 key)
        {
            return this.collection.ContainsKey(key.ToByteArray());
        }

        public bool TryGetValue(UInt256 key, out TValue value)
        {
            byte[] bytes;
            if (this.collection.TryGetValue(key.ToByteArray(), out bytes))
            {
                value = this.decoder(key, bytes);
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        public bool TryAdd(UInt256 key, TValue value)
        {
            return this.collection.TryAdd(key.ToByteArray(), this.encoder(value));
        }

        public bool TryRemove(UInt256 key)
        {
            return this.collection.TryRemove(key.ToByteArray());
        }

        public TValue this[UInt256 key]
        {
            get
            {
                return this.decoder(key, this.collection[key.ToByteArray()]);
            }
            set
            {
                this.collection[key.ToByteArray()] = this.encoder(value);
            }
        }

        public void Flush()
        {
            throw new NotSupportedException();
        }

        public IEnumerator<KeyValuePair<UInt256, TValue>> GetEnumerator()
        {
            return this.collection.Select(x => new KeyValuePair<UInt256, TValue>(new UInt256(x.Key), this.decoder(new UInt256(x.Key), x.Value))).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
