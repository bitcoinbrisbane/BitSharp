using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Core.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent.Test
{
    [TestClass]
    public class MemoryStorageProviderTest : StorageProviderTest
    {
        private string baseDirectory;

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.baseDirectory))
                Directory.Delete(this.baseDirectory, recursive: true);
        }

        [TestInitialize]
        public void Initialize()
        {
            this.baseDirectory = Path.Combine(Path.GetTempPath(), "BitSharp", "Tests");

            if (Directory.Exists(this.baseDirectory))
                Directory.Delete(this.baseDirectory, recursive: true);

            Directory.CreateDirectory(this.baseDirectory);
        }

        public override IBlockStorageNew OpenBlockStorage()
        {
            return new MemoryBlockStorageNew();
        }

        public override IChainStateBuilderStorage OpenChainStateBuilderStorage(IChainStateStorage parentUtxo, Logger logger)
        {
            return new MemoryChainStateBuilderStorage(parentUtxo);
        }

        [TestMethod]
        public void TestMemoryPrune()
        {
            base.TestPrune();
        }

        [TestMethod]
        public void TestMemoryRollback()
        {
            base.TestRollback();
        }
    }
}
