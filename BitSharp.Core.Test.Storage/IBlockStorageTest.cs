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

        private void TestContainsChainedHeader(ITestStorageProvider provider)
        {
            Assert.Inconclusive("TODO");
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
