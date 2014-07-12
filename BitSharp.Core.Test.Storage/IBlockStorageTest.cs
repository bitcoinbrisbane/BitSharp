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

        // IBlockStorage.TryAddChainedHeader
        private void TestTryAddChainedHeader(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockStorage = storageManager.BlockStorage;

                // create a chained header
                var fakeHeaders = new FakeHeaders();
                var chainedHeader = fakeHeaders.GenesisChained();

                // verify header can be added
                Assert.IsTrue(blockStorage.TryAddChainedHeader(chainedHeader));

                // verify header cannot be added again
                Assert.IsFalse(blockStorage.TryAddChainedHeader(chainedHeader));

                // remove the header
                blockStorage.TryRemoveChainedHeader(chainedHeader.Hash);

                // verify header can be added again, after being removed
                Assert.IsTrue(blockStorage.TryAddChainedHeader(chainedHeader));
            }
        }

        // IBlockStorage.TryGetChainedHeader
        private void TestTryGetChainedHeader(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockStorage = storageManager.BlockStorage;

                // create a chained header
                var fakeHeaders = new FakeHeaders();
                var expectedChainedHeader = fakeHeaders.GenesisChained();

                // add header
                blockStorage.TryAddChainedHeader(expectedChainedHeader);

                // retrieve header
                ChainedHeader actualChainedHeader;
                Assert.IsTrue(blockStorage.TryGetChainedHeader(expectedChainedHeader.Hash, out actualChainedHeader));

                // verify retrieved header matches stored header
                Assert.AreEqual(expectedChainedHeader, actualChainedHeader);
            }
        }

        // IBlockStorage.FindMaxTotalWork
        private void TestFindMaxTotalWork(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockStorage = storageManager.BlockStorage;

                // create a chained headers
                var fakeHeaders = new FakeHeaders();
                var chainedHeader0 = fakeHeaders.GenesisChained();
                var chainedHeader1 = fakeHeaders.NextChained();
                var chainedHeader2 = fakeHeaders.NextChained();

                // verify initial null state
                Assert.IsNull(blockStorage.FindMaxTotalWork());

                // add headers and verify max total work

                // 0
                blockStorage.TryAddChainedHeader(chainedHeader0);
                Assert.AreEqual(chainedHeader0, blockStorage.FindMaxTotalWork());

                // 1
                blockStorage.TryAddChainedHeader(chainedHeader1);
                Assert.AreEqual(chainedHeader1, blockStorage.FindMaxTotalWork());

                // 2
                blockStorage.TryAddChainedHeader(chainedHeader2);
                Assert.AreEqual(chainedHeader2, blockStorage.FindMaxTotalWork());

                // remove headers and verify max total work

                // 2
                blockStorage.TryRemoveChainedHeader(chainedHeader2.Hash);
                Assert.AreEqual(chainedHeader1, blockStorage.FindMaxTotalWork());

                // 1
                blockStorage.TryRemoveChainedHeader(chainedHeader1.Hash);
                Assert.AreEqual(chainedHeader0, blockStorage.FindMaxTotalWork());

                // 0
                blockStorage.TryRemoveChainedHeader(chainedHeader0.Hash);
                Assert.IsNull(blockStorage.FindMaxTotalWork());
            }
        }

        // IBlockStorage.ReadChainedHeaders
        private void TestReadChainedHeaders(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockStorage = storageManager.BlockStorage;

                // create a chained headers
                var fakeHeaders = new FakeHeaders();
                var chainedHeader0 = fakeHeaders.GenesisChained();
                var chainedHeader1 = fakeHeaders.NextChained();
                var chainedHeader2 = fakeHeaders.NextChained();

                // verify initial empty state
                Assert.AreEqual(0, blockStorage.ReadChainedHeaders().ToList().Count);

                // add headers and verify reading them

                // 0
                blockStorage.TryAddChainedHeader(chainedHeader0);
                CollectionAssert.AreEquivalent(new[] { chainedHeader0 }, blockStorage.ReadChainedHeaders().ToList());

                // 1
                blockStorage.TryAddChainedHeader(chainedHeader1);
                CollectionAssert.AreEquivalent(new[] { chainedHeader0, chainedHeader1 }, blockStorage.ReadChainedHeaders().ToList());

                // 2
                blockStorage.TryAddChainedHeader(chainedHeader2);
                CollectionAssert.AreEquivalent(new[] { chainedHeader0, chainedHeader1, chainedHeader2 }, blockStorage.ReadChainedHeaders().ToList());

                // remove headers and verify reading them

                // 2
                blockStorage.TryRemoveChainedHeader(chainedHeader2.Hash);
                CollectionAssert.AreEquivalent(new[] { chainedHeader0, chainedHeader1 }, blockStorage.ReadChainedHeaders().ToList());

                // 1
                blockStorage.TryRemoveChainedHeader(chainedHeader1.Hash);
                CollectionAssert.AreEquivalent(new[] { chainedHeader0 }, blockStorage.ReadChainedHeaders().ToList());

                // 0
                blockStorage.TryRemoveChainedHeader(chainedHeader0.Hash);
                Assert.AreEqual(0, blockStorage.ReadChainedHeaders().ToList().Count);
            }
        }

        // IBlockStorage.IsBlockInvalid
        private void TestIsBlockInvalid(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockStorage = storageManager.BlockStorage;

            }
            Assert.Inconclusive("TODO");
        }

        // IBlockStorage.MarkBlockInvalid
        private void TestMarkBlockInvalid(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockStorage = storageManager.BlockStorage;

            }
            Assert.Inconclusive("TODO");
        }
    }
}
