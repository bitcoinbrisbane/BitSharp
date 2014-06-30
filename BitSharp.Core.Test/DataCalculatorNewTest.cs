using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test
{
    [TestClass]
    public class DataCalculatorNewTest
    {
        [TestMethod]
        public void TestReadBlockElements()
        {
            var sha256 = new SHA256Managed();

            var blockHash = (UInt256)0;

            var hash1 = (UInt256)1;
            var blockElement1 = new BlockElement(blockHash, index: 0, depth: 0, hash: hash1);

            var hash2 = (UInt256)2;
            var blockElement2 = new BlockElement(blockHash, index: 1, depth: 0, hash: hash2);

            var merkleRoot = new UInt256(sha256.ComputeDoubleHash(hash1.ToByteArray().Concat(hash2.ToByteArray())));

            var blockElements = new List<BlockElement> { blockElement1, blockElement2 };

            var readElements = DataCalculatorNew.ReadBlockElements(merkleRoot, blockElements).ToList();

            CollectionAssert.AreEqual(blockElements, readElements);
        }
    }
}
