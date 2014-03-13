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
using BitSharp.Storage.SqlServer.ExtensionMethods;
using System.Threading;
using System.Diagnostics;

namespace BitSharp.Storage.SqlServer
{
    public abstract class SqlDataStorage : SqlBase, IDisposable
    {
        private readonly SqlServerStorageContext storageContext;

        public SqlDataStorage(SqlServerStorageContext storageContext)
        {
            this.storageContext = storageContext;
        }

        public SqlServerStorageContext StorageContext { get { return this.storageContext; } }

        public void Dispose()
        {
        }
    }
}
