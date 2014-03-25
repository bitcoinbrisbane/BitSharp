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
        private readonly PersistentByteDictionary dict;

        private readonly Func<TValue, byte[]> encoder;
        private readonly Func<UInt256, byte[], TValue> decoder;

        public EsentDataStorage(EsentStorageContext storageContext, string name, Func<TValue, byte[]> encoder, Func<UInt256, byte[], TValue> decoder)
        {
            this.storageContext = storageContext;
            this.name = name;
            this.directory = Path.Combine(storageContext.BaseDirectory, name);
            this.dict = new PersistentByteDictionary(this.directory);

            this.encoder = encoder;
            this.decoder = decoder;
        }

        public EsentStorageContext StorageContext { get { return this.storageContext; } }

        public void Dispose()
        {
            this.dict.Dispose();
        }

        protected PersistentByteDictionary Data { get { return this.dict; } }

        public int Count
        {
            get { return this.dict.Count; }
        }

        public IEnumerable<UInt256> Keys
        {
            get { return this.dict.Keys.Select(x => new UInt256(x)); }
        }

        public IEnumerable<TValue> Values
        {
            get { return this.dict.Select(x => this.decoder(new UInt256(x.Key), x.Value)); }
        }

        public bool ContainsKey(UInt256 key)
        {
            return this.dict.ContainsKey(key.ToByteArray());
        }

        public bool TryGetValue(UInt256 key, out TValue value)
        {
            byte[] bytes;
            if (this.dict.TryGetValue(key.ToByteArray(), out bytes))
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
            if (!this.ContainsKey(key))
            {
                try
                {
                    this.dict.Add(key.ToByteArray(), this.encoder(value));
                    return true;
                }
                catch (ArgumentException)
                { return false; }
            }
            else
            {
                return false;
            }
        }

        public TValue this[UInt256 key]
        {
            get
            {
                return this.decoder(key, this.dict[key.ToByteArray()]);
            }
            set
            {
                this.dict[key.ToByteArray()] = this.encoder(value);
            }
        }

        public IEnumerator<KeyValuePair<UInt256, TValue>> GetEnumerator()
        {
            foreach (var keyPair in this.dict)
                yield return new KeyValuePair<UInt256, TValue>(new UInt256(keyPair.Key), this.decoder(new UInt256(keyPair.Key), keyPair.Value));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
