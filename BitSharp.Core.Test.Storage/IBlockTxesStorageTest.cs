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

                // create blocks
                var fakeBlock0 = CreateFakeBlock();
                var fakeBlock1 = CreateFakeBlock();
                var fakeBlock2 = CreateFakeBlock();

                // verify initial count of 0
                Assert.AreEqual(0, blockTxesStorage.BlockCount);

                // add blocks and verify count

                // 0
                blockTxesStorage.TryAddBlockTransactions(fakeBlock0.Hash, fakeBlock0.Transactions);
                Assert.AreEqual(1, blockTxesStorage.BlockCount);

                // 1
                blockTxesStorage.TryAddBlockTransactions(fakeBlock1.Hash, fakeBlock1.Transactions);
                Assert.AreEqual(2, blockTxesStorage.BlockCount);

                // 2
                blockTxesStorage.TryAddBlockTransactions(fakeBlock2.Hash, fakeBlock2.Transactions);
                Assert.AreEqual(3, blockTxesStorage.BlockCount);

                // remove blocks and verify count

                // 0
                blockTxesStorage.TryRemoveBlockTransactions(fakeBlock0.Hash);
                Assert.AreEqual(2, blockTxesStorage.BlockCount);

                // 1
                blockTxesStorage.TryRemoveBlockTransactions(fakeBlock1.Hash);
                Assert.AreEqual(1, blockTxesStorage.BlockCount);

                // 2
                blockTxesStorage.TryRemoveBlockTransactions(fakeBlock2.Hash);
                Assert.AreEqual(0, blockTxesStorage.BlockCount);
            }
        }

        // IBlockTxesStorage.ContainsBlock
        private void TestContainsBlock(ITestStorageProvider provider)
        {
            using (var storageManager = provider.OpenStorageManager())
            {
                var blockTxesStorage = storageManager.BlockTxesStorage;

                // create a block
                var block = CreateFakeBlock();

                // block should not be present
                Assert.IsFalse(blockTxesStorage.ContainsBlock(block.Hash));

                // add the block
                blockTxesStorage.TryAddBlockTransactions(block.Hash, block.Transactions);

                // block should be present
                Assert.IsTrue(blockTxesStorage.ContainsBlock(block.Hash)); ;

                // remove the block
                blockTxesStorage.TryRemoveBlockTransactions(block.Hash);

                // block should not be present
                Assert.IsFalse(blockTxesStorage.ContainsBlock(block.Hash)); ;
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

        private Block CreateFakeBlock()
        {
            var txCount = 100;
            var transactions = Enumerable.Range(0, txCount).Select(x => RandomData.RandomTransaction()).ToImmutableArray();
            var blockHeader = RandomData.RandomBlockHeader().With(MerkleRoot: MerkleTree.CalculateMerkleRoot(transactions), Bits: DataCalculator.TargetToBits(UnitTestRules.Target0));
            var block = new Block(blockHeader, transactions);

            return block;
        }
    }
}
