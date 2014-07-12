using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Test;
using BitSharp.Core.Test.Rules;
using BitSharp.Domain;
using BitSharp.Esent.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test.Storage
{
    [TestClass]
    public class IBlockStorageTest : StorageProviderTest
    {
        [TestMethod]
        public void TestContainsChainedHeader()
        {
            RunTest(TestContainsChainedHeader);
        }

        [TestMethod]
        public void TestTryAddChainedHeader()
        {
            RunTest(TestTryAddChainedHeader);
        }

        [TestMethod]
        public void TestTryGetChainedHeader()
        {
            RunTest(TestTryGetChainedHeader);
        }

        [TestMethod]
        public void TestFindMaxTotalWork()
        {
            RunTest(TestFindMaxTotalWork);
        }

        [TestMethod]
        public void TestReadChainedHeaders()
        {
            RunTest(TestReadChainedHeaders);
        }

        [TestMethod]
        public void TestIsBlockInvalid()
        {
            RunTest(TestIsBlockInvalid);
        }

        [TestMethod]
        public void TestMarkBlockInvalid()
        {
            RunTest(TestMarkBlockInvalid);
        }

        // IBlockStorage.ContainsChainedHeader
        private void TestContainsChainedHeader(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockStorage = storageManager.BlockStorage;

                // create a chained header
                var fakeHeaders = new FakeHeaders();
                var chainedHeader = fakeHeaders.GenesisChained();

                // header should not be present
                Assert.IsFalse(blockStorage.ContainsChainedHeader(chainedHeader.Hash));

                // add the header
                blockStorage.TryAddChainedHeader(chainedHeader);

                // header should be present
                Assert.IsTrue(blockStorage.ContainsChainedHeader(chainedHeader.Hash)); ;

                // remove the header
                blockStorage.TryRemoveChainedHeader(chainedHeader.Hash);

                // header should not be present
                Assert.IsFalse(blockStorage.ContainsChainedHeader(chainedHeader.Hash)); ;
            }
        }

        private void TestTryAddChainedHeader(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestTryGetChainedHeader(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestFindMaxTotalWork(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestReadChainedHeaders(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestIsBlockInvalid(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestMarkBlockInvalid(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }
    }
}
