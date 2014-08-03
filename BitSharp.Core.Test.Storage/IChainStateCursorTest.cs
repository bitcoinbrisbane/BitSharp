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
    public class IChainStateCursorTest : StorageProviderTest
    {
        [TestMethod]
        public void TestTransactionIsolation()
        {
            RunTest(TestTransactionIsolation);
        }

        [TestMethod]
        public void TestBeginTransaction()
        {
            RunTest(TestBeginTransaction);
        }

        [TestMethod]
        public void TestCommitTransaction()
        {
            RunTest(TestCommitTransaction);
        }

        [TestMethod]
        public void TestRollbackTransaction()
        {
            RunTest(TestRollbackTransaction);
        }

        [TestMethod]
        public void TestReadChain()
        {
            RunTest(TestReadChain);
        }

        [TestMethod]
        public void TestAddChainedHeader()
        {
            RunTest(TestAddChainedHeader);
        }

        [TestMethod]
        public void TestRemoveChainedHeader()
        {
            RunTest(TestRemoveChainedHeader);
        }

        [TestMethod]
        public void TestUnspentTxCount()
        {
            RunTest(TestUnspentTxCount);
        }

        [TestMethod]
        public void TestConainsUnspentTx()
        {
            RunTest(TestConainsUnspentTx);
        }

        [TestMethod]
        public void TestTryGetUnspentTx()
        {
            RunTest(TestTryGetUnspentTx);
        }

        [TestMethod]
        public void TestTryAddUnspentTx()
        {
            RunTest(TestTryAddUnspentTx);
        }

        [TestMethod]
        public void TestTryRemoveUnspentTx()
        {
            RunTest(TestTryRemoveUnspentTx);
        }

        [TestMethod]
        public void TestTryUpdateUnspentTx()
        {
            RunTest(TestTryUpdateUnspentTx);
        }

        private void TestTransactionIsolation(ITestStorageProvider provider)
        {
            var fakeHeaders = new FakeHeaders();
            var chainedHeader0 = fakeHeaders.GenesisChained();

            // open two chain state cursors
            using (var storageManager = provider.OpenStorageManager())
            using (var chainStateCursor1 = storageManager.OpenChainStateCursor())
            using (var chainStateCursor2 = storageManager.OpenChainStateCursor())
            {
                // open transactions on both cursors
                chainStateCursor1.BeginTransaction();
                chainStateCursor2.BeginTransaction();

                // verify initial empty chain
                Assert.AreEqual(0, chainStateCursor1.ReadChain().Count());
                Assert.AreEqual(0, chainStateCursor2.ReadChain().Count());

                // add a header on cursor 1
                chainStateCursor1.AddChainedHeader(chainedHeader0);

                // verify cursor 1 sees the new header while cursor 2 does not
                CollectionAssert.AreEqual(new[] { chainedHeader0 }, chainStateCursor1.ReadChain().ToList());
                Assert.AreEqual(0, chainStateCursor2.ReadChain().Count());

                // commit cursor 1
                chainStateCursor1.CommitTransaction();

                // verify cursor 1 sees the new header while cursor 2 does not
                CollectionAssert.AreEqual(new[] { chainedHeader0 }, chainStateCursor1.ReadChain().ToList());
                Assert.AreEqual(0, chainStateCursor2.ReadChain().Count());

                // commit cursor 2
                chainStateCursor2.CommitTransaction();

                // verify cursor 2 now sees the new header
                CollectionAssert.AreEqual(new[] { chainedHeader0 }, chainStateCursor2.ReadChain().ToList());
            }
        }

        private void TestBeginTransaction(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestCommitTransaction(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestRollbackTransaction(ITestStorageProvider provider)
        {
            var fakeHeaders = new FakeHeaders();
            var chainedHeader0 = fakeHeaders.GenesisChained();
            var chainedHeader1 = fakeHeaders.NextChained();
            var chainedHeader2 = fakeHeaders.NextChained();

            var unspentTx = new UnspentTx(txHash: 0, blockIndex: 0, txIndex: 0, outputStates: new OutputStates(1, OutputState.Unspent));

            var spentTxes = new List<SpentTx> { new SpentTx(txHash: 1, confirmedBlockIndex: 0, txIndex: 0, outputCount: 1, spentBlockIndex: 0) };

            using (var storageManager = provider.OpenStorageManager())
            using (var chainStateCursor = storageManager.OpenChainStateCursor())
            {
                // begin transaction
                chainStateCursor.BeginTransaction();

                // add header 0
                chainStateCursor.AddChainedHeader(chainedHeader0);

                // verify chain
                CollectionAssert.AreEqual(new[] { chainedHeader0 }, chainStateCursor.ReadChain().ToList());

                // add unspent tx
                chainStateCursor.TryAddUnspentTx(unspentTx);

                // verify unspent tx
                Assert.IsTrue(chainStateCursor.ContainsUnspentTx(unspentTx.TxHash));
                Assert.AreEqual(1, chainStateCursor.UnspentTxCount);

                // add spent txes
                chainStateCursor.PrepareSpentTransactions(0);
                chainStateCursor.AddSpentTransaction(spentTxes.First());

                // verify spent txes
                CollectionAssert.AreEqual(spentTxes, chainStateCursor.ReadSpentTransactions(0).ToList());

                // rollback transaction
                chainStateCursor.RollbackTransaction();

                // verify chain
                Assert.AreEqual(0, chainStateCursor.ReadChain().Count());

                // verify unspent tx
                Assert.IsFalse(chainStateCursor.ContainsUnspentTx(unspentTx.TxHash));
                Assert.AreEqual(0, chainStateCursor.UnspentTxCount);

                // verify spent txes
                Assert.AreEqual(0, chainStateCursor.ReadSpentTransactions(0).Count());
            }
        }

        private void TestReadChain(ITestStorageProvider provider)
        {
            var fakeHeaders = new FakeHeaders();
            var chainedHeader0 = fakeHeaders.GenesisChained();
            var chainedHeader1 = fakeHeaders.NextChained();
            var chainedHeader2 = fakeHeaders.NextChained();

            using (var storageManager = provider.OpenStorageManager())
            using (var chainStateCursor = storageManager.OpenChainStateCursor())
            {
                chainStateCursor.BeginTransaction();

                // verify initial empty chain
                Assert.AreEqual(0, chainStateCursor.ReadChain().Count());

                // add header 0
                chainStateCursor.AddChainedHeader(chainedHeader0);

                // verify chain
                CollectionAssert.AreEqual(new[] { chainedHeader0 }, chainStateCursor.ReadChain().ToList());

                // add header 1
                chainStateCursor.AddChainedHeader(chainedHeader1);

                // verify chain
                CollectionAssert.AreEqual(new[] { chainedHeader0, chainedHeader1 }, chainStateCursor.ReadChain().ToList());

                // add header 2
                chainStateCursor.AddChainedHeader(chainedHeader2);

                // verify chain
                CollectionAssert.AreEqual(new[] { chainedHeader0, chainedHeader1, chainedHeader2 }, chainStateCursor.ReadChain().ToList());

                // remove header 2
                chainStateCursor.RemoveChainedHeader(chainedHeader2);

                // verify chain
                CollectionAssert.AreEqual(new[] { chainedHeader0, chainedHeader1 }, chainStateCursor.ReadChain().ToList());

                // remove header 1
                chainStateCursor.RemoveChainedHeader(chainedHeader1);

                // verify chain
                CollectionAssert.AreEqual(new[] { chainedHeader0 }, chainStateCursor.ReadChain().ToList());

                // remove header 0
                chainStateCursor.RemoveChainedHeader(chainedHeader0);

                // verify chain
                Assert.AreEqual(0, chainStateCursor.ReadChain().Count());
            }
        }

        private void TestAddChainedHeader(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestRemoveChainedHeader(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestUnspentTxCount(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestConainsUnspentTx(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestTryGetUnspentTx(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestTryAddUnspentTx(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestTryRemoveUnspentTx(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }

        private void TestTryUpdateUnspentTx(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
        }
    }
}
