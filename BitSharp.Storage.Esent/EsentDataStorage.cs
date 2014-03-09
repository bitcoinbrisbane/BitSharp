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

namespace BitSharp.Storage.Esent
{
    public abstract class EsentDataStorage : IDisposable
    {
        private readonly EsentStorageContext _storageContext;
        private readonly string _name;
        private readonly string _dataPath;
        private readonly PersistentDictionary<string, string> _data;

        public EsentDataStorage(EsentStorageContext storageContext, string name)
        {
            this._storageContext = storageContext;
            this._name = name;
            this._dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "data", name);
            this._data = new PersistentDictionary<string, string>(this._dataPath);
        }

        public EsentStorageContext StorageContext { get { return this._storageContext; } }

        public void Dispose()
        {
            this._data.Dispose();
        }

        protected PersistentDictionary<string, string> Data { get { return this._data; } }

        protected IEnumerable<byte[]> ReadAllDataKeys()
        {
            foreach (var key in this._data.Keys)
                yield return Convert.FromBase64String(key);
        }

        protected IEnumerable<KeyValuePair<byte[], byte[]>> ReadAllDataValues()
        {
            foreach (var keyPair in this._data)
                yield return new KeyValuePair<byte[], byte[]>(Convert.FromBase64String(keyPair.Key), Convert.FromBase64String(keyPair.Value));
        }

        protected bool TryReadDataValue(byte[] key, out byte[] value)
        {
            var keyString = Convert.ToBase64String(key);
            string valueString;
            if (this._data.TryGetValue(keyString, out valueString))
            {
                value = Convert.FromBase64String(valueString);
                return true;
            }
            else
            {
                value = default(byte[]);
                return false;
            }
        }

        protected bool TryWriteDataValues(IEnumerable<KeyValuePair<byte[], WriteValue<byte[]>>> keyPairs)
        {
            foreach (var keyPair in keyPairs)
            {
                var keyString = Convert.ToBase64String(keyPair.Key);
                var valueString = Convert.ToBase64String(keyPair.Value.Value);

                this._data[keyString] = valueString;
            }
            
            return true;
        }

        protected void TruncateData()
        {
            this._data.Clear();
        }
    }
}
