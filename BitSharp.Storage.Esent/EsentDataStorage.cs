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
        private readonly PersistentUInt256ByteDictionary _data;

        public EsentDataStorage(EsentStorageContext storageContext, string name)
        {
            this._storageContext = storageContext;
            this._name = name;
            this._dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "data", name);
            this._data = new PersistentUInt256ByteDictionary(this._dataPath);
        }

        public EsentStorageContext StorageContext { get { return this._storageContext; } }

        public void Dispose()
        {
            this._data.Dispose();
        }

        protected PersistentUInt256ByteDictionary Data { get { return this._data; } }
    }
}
