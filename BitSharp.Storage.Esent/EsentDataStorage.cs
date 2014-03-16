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
using Microsoft.Isam.Esent.Collections.Generic;
using BitSharp.Common;

namespace BitSharp.Storage.Esent
{
    public abstract class EsentDataStorage<TValue> : IBoundedStorage<UInt256, TValue>
    {
        private readonly EsentStorageContext storageContext;
        private readonly string name;
        private readonly string directory;
        private readonly PersistentUInt256ByteDictionary dict;

        private readonly Func<TValue, byte[]> encoder;
        private readonly Func<UInt256, byte[], TValue> decoder;

        public EsentDataStorage(EsentStorageContext storageContext, string name, Func<TValue, byte[]> encoder, Func<UInt256, byte[], TValue> decoder)
        {
            this.storageContext = storageContext;
            this.name = name;
            this.directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "data", name);
            this.dict = new PersistentUInt256ByteDictionary(this.directory);

            this.encoder = encoder;
            this.decoder = decoder;
        }

        public EsentStorageContext StorageContext { get { return this.storageContext; } }

        public void Dispose()
        {
            this.dict.Dispose();
        }

        protected PersistentUInt256ByteDictionary Data { get { return this.dict; } }

        public int Count
        {
            get { return this.dict.Count; }
        }

        public ICollection<UInt256> Keys
        {
            get { return this.dict.Keys; }
        }

        public ICollection<TValue> Values
        {
            get { return new SimpleCollection<TValue>(() => this.Count, () => this.Select(x => x.Value).GetEnumerator()); }
        }

        public bool ContainsKey(UInt256 key)
        {
            return this.dict.ContainsKey(key);
        }

        public bool TryGetValue(UInt256 key, out TValue value)
        {
            byte[] bytes;
            if (this.dict.TryGetValue(key, out bytes))
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
            try
            {
                this.dict.Add(key, this.encoder(value));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public TValue this[UInt256 key]
        {
            get
            {
                return this.decoder(key, this.dict[key]);
            }
            set
            {
                this.dict[key] = this.encoder(value);
            }
        }

        public IEnumerator<KeyValuePair<UInt256, TValue>> GetEnumerator()
        {
            foreach (var keyPair in this.dict)
                yield return new KeyValuePair<UInt256, TValue>(keyPair.Key, this.decoder(keyPair.Key, keyPair.Value));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private class ValuesCollection : ICollection<TValue>
        {
            private readonly EsentDataStorage<TValue> storage;

            public ValuesCollection(EsentDataStorage<TValue> storage)
            {
                this.storage = storage;
            }

            public void Add(TValue item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(TValue item)
            {
                throw new NotSupportedException();
            }

            public void CopyTo(TValue[] array, int arrayIndex)
            {
                throw new NotSupportedException();
            }

            public int Count
            {
                get { return this.storage.Count; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            public bool Remove(TValue item)
            {
                throw new NotSupportedException();
            }

            public IEnumerator<TValue> GetEnumerator()
            {
                return this.storage.Select(x => x.Value).GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}
