using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Core.Test.Storage;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test.Storage
{
    public class MemoryTestStorageProvider : ITestStorageProvider
    {
        public string Name { get { return "Memory Storage"; } }

        public void TestInitialize() { }

        public void TestCleanup() { }

        public IStorageManager OpenStorageManager()
        {
            return new MemoryStorageManager();
        }
    }
}
