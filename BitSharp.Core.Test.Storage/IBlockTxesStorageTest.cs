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
    public class IBlockTxesStorageTest : StorageProviderTest
    {
        [TestMethod]
        public void TestBlockCount()
        {
            RunTest(TestBlockCount);
        }

        [TestMethod]
        public void TestContainsBlock()
        {
            RunTest(TestContainsBlock);
        }

        [TestMethod]
        public void TestTryAdd()
        {
            RunTest(TestTryAdd);
        }

        [TestMethod]
        public void TestTryGetTransaction()
        {
            RunTest(TestTryGetTransaction);
        }

        [TestMethod]
        public void TestTryRemove()
        {
            RunTest(TestTryRemove);
        }

        [TestMethod]
        public void TestReadBlockTransactions()
        {
            RunTest(TestReadBlockTransactions);
        }

        [TestMethod]
        public void TestPruneElements()
        {
            RunTest(TestPruneElements);
        }

        [TestMethod]
        public void TestFlush()
        {
            RunTest(TestFlush);
        }

        [TestMethod]
        public void TestDefragment()
        {
            RunTest(TestDefragment);
        }

        // IBlockTxesStorage.BlockCount
        private void TestBlockCount(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }

        // IBlockTxesStorage.ContainsBlock
        private void TestContainsBlock(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }

        // IBlockTxesStorage.TryAdd
        private void TestTryAdd(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }

        // IBlockTxesStorage.TryGetTransaction
        private void TestTryGetTransaction(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }

        // IBlockTxesStorage.TryRemove
        private void TestTryRemove(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }

        // IBlockTxesStorage.ReadBlockTransactions
        private void TestReadBlockTransactions(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }

        // IBlockTxesStorage.PruneElements
        private void TestPruneElements(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }

        // IBlockTxesStorage.Flush
        private void TestFlush(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }

        // IBlockTxesStorage.Defragment
        private void TestDefragment(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                Assert.Inconclusive("TODO");
            }
        }
    }
}
