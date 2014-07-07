using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
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
    public class EsentStorageProviderTest : StorageProviderTest
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
            return new BlockStorageNew(this.baseDirectory);
        }

        public override IChainStateBuilderStorage OpenChainStateBuilderStorage(IChainStateStorage parentUtxo, Logger logger)
        {
            return new ChainStateBuilderStorage(this.baseDirectory, parentUtxo, logger);
        }

        [TestMethod]
        public void TestEsentPrune()
        {
            base.TestPrune();
        }

        [TestMethod]
        public void TestEsentRollback()
        {
            base.TestRollback();
        }
    }
}
