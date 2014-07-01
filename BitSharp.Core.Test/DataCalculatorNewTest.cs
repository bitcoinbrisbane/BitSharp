using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

            var blockElement1 = new BlockElement(blockHash, index: 0, depth: 0, hash: 1);
            var blockElement2 = new BlockElement(blockHash, index: 1, depth: 0, hash: 2);
            var blockElement3 = new BlockElement(blockHash, index: 2, depth: 0, hash: 3);
            var blockElement4 = new BlockElement(blockHash, index: 3, depth: 0, hash: 4);

            var depth1Hash1 = DataCalculatorNew.PairHashes(blockElement1.Hash, blockElement2.Hash);
            var depth1Hash2 = DataCalculatorNew.PairHashes(blockElement3.Hash, blockElement4.Hash);
            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var blockElements = new List<BlockElement> { blockElement1, blockElement2, blockElement3, blockElement4 };

            var readElements = DataCalculatorNew.ReadBlockElements(blockHash, merkleRoot, blockElements).ToList();

            CollectionAssert.AreEqual(blockElements, readElements);
        }

        [TestMethod]
        public void TestReadBlockElementsOddPower()
        {
            var sha256 = new SHA256Managed();

            var blockHash = (UInt256)0;

            var depth0Hash1 = (UInt256)1;
            var depth0Hash2 = (UInt256)2;
            var depth0Hash3 = (UInt256)3;

            var depth1Hash1 = DataCalculatorNew.PairHashes(depth0Hash1, depth0Hash2);
            var depth1Hash2 = DataCalculatorNew.PairHashes(depth0Hash3, depth0Hash3);

            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var blockElements = new List<BlockElement> { 
                new BlockElement(blockHash, index: 0, depth: 0, hash: depth0Hash1),
                new BlockElement(blockHash, index: 1, depth: 0, hash: depth0Hash2),
                new BlockElement(blockHash, index: 2, depth: 0, hash: depth0Hash3),
            };

            var readElements = DataCalculatorNew.ReadBlockElements(blockHash, merkleRoot, blockElements).ToList();

            CollectionAssert.AreEqual(blockElements, readElements);
        }

        [TestMethod]
        public void TestReadBlockElementsPruned()
        {
            var sha256 = new SHA256Managed();

            var blockHash = (UInt256)0;

            var depth0Hash1 = (UInt256)1;
            var depth0Hash2 = (UInt256)2;
            var depth0Hash3 = (UInt256)3;
            var depth0Hash4 = (UInt256)4;
            var depth0Hash5 = (UInt256)5;
            var depth0Hash6 = (UInt256)6;
            var depth0Hash7 = (UInt256)7;
            var depth0Hash8 = (UInt256)8;
            var depth0Hash9 = (UInt256)9;
            var depth0Hash10 = (UInt256)10;
            var depth0Hash11 = (UInt256)11;
            var depth0Hash12 = (UInt256)12;
            var depth0Hash13 = (UInt256)13;
            var depth0Hash14 = (UInt256)14;
            var depth0Hash15 = (UInt256)15;

            var depth1Hash1 = DataCalculatorNew.PairHashes(depth0Hash1, depth0Hash2);
            var depth1Hash2 = DataCalculatorNew.PairHashes(depth0Hash3, depth0Hash4);
            var depth1Hash3 = DataCalculatorNew.PairHashes(depth0Hash5, depth0Hash6);
            var depth1Hash4 = DataCalculatorNew.PairHashes(depth0Hash7, depth0Hash8);
            var depth1Hash5 = DataCalculatorNew.PairHashes(depth0Hash9, depth0Hash10);
            var depth1Hash6 = DataCalculatorNew.PairHashes(depth0Hash11, depth0Hash12);
            var depth1Hash7 = DataCalculatorNew.PairHashes(depth0Hash13, depth0Hash14);
            var depth1Hash8 = DataCalculatorNew.PairHashes(depth0Hash15, depth0Hash15);

            var depth2Hash1 = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);
            var depth2Hash2 = DataCalculatorNew.PairHashes(depth1Hash3, depth1Hash4);
            var depth2Hash3 = DataCalculatorNew.PairHashes(depth1Hash5, depth1Hash6);
            var depth2Hash4 = DataCalculatorNew.PairHashes(depth1Hash7, depth1Hash8);

            var depth3Hash1 = DataCalculatorNew.PairHashes(depth2Hash1, depth2Hash2);
            var depth3Hash2 = DataCalculatorNew.PairHashes(depth2Hash3, depth2Hash4);

            var merkleRoot = DataCalculatorNew.PairHashes(depth3Hash1, depth3Hash2);

            var blockElements = new List<BlockElement> { 
                new BlockElement(blockHash, index: 0, depth: 0, hash: depth0Hash1),
                new BlockElement(blockHash, index: 1, depth: 0, hash: depth0Hash2),
                new BlockElement(blockHash, index: 2, depth: 0, hash: depth0Hash3),
                new BlockElement(blockHash, index: 3, depth: 0, hash: depth0Hash4),
                        new BlockElement(blockHash, index: 4, depth: 2, hash: depth2Hash2),
                new BlockElement(blockHash, index: 8, depth: 0, hash: depth0Hash9),
                new BlockElement(blockHash, index: 9, depth: 0, hash: depth0Hash10),
                    new BlockElement(blockHash, index: 10, depth: 1, hash: depth1Hash6),
                new BlockElement(blockHash, index: 12, depth: 0, hash: depth0Hash13),
                new BlockElement(blockHash, index: 13, depth: 0, hash: depth0Hash14),
                    new BlockElement(blockHash, index: 14, depth: 1, hash: depth1Hash8),
            };

            var readElements = DataCalculatorNew.ReadBlockElements(blockHash, merkleRoot, blockElements).ToList();

            CollectionAssert.AreEqual(blockElements, readElements);
        }

        [TestMethod]
        public void TestReadBlockElementsPerformance()
        {
            var sha256 = new SHA256Managed();

            var blockHash = (UInt256)0;

            var count = 10.THOUSAND();
            var blockElements = new List<BlockElement>(count);
            for (var i = 0; i < count; i++)
            {
                blockElements.Add(new BlockElement(blockHash, i, depth: 0, hash: i));
            }

            var merkleRoot = new MethodTimer().Time(() =>
                DataCalculator.CalculateMerkleRoot(blockElements.Select(x => x.Hash).ToImmutableList()));

            var readElements = new MethodTimer().Time(() =>
                DataCalculatorNew.ReadBlockElements(blockHash, merkleRoot, blockElements).ToList());

            CollectionAssert.AreEqual(blockElements, readElements);
        }

        [TestMethod]
        [ExpectedException(typeof(ValidationException))]
        public void TestReadBlockElementsBadDepth()
        {
            var sha256 = new SHA256Managed();

            var blockHash = (UInt256)0;

            var depth0Hash1 = (UInt256)1;
            var depth0Hash2 = (UInt256)2;
            var depth0Hash3 = (UInt256)3;

            var depth1Hash1 = DataCalculatorNew.PairHashes(depth0Hash1, depth0Hash2);
            var depth1Hash2 = DataCalculatorNew.PairHashes(depth0Hash3, depth0Hash3);

            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var blockElements = new List<BlockElement> { 
                new BlockElement(blockHash, index: 0, depth: 0, hash: depth0Hash1),
                new BlockElement(blockHash, index: 1, depth: 0, hash: depth0Hash2),
                new BlockElement(blockHash, index: 2, depth: 1, hash: depth0Hash3),
            };

            var readElements = DataCalculatorNew.ReadBlockElements(blockHash, merkleRoot, blockElements).ToList();

            CollectionAssert.AreEqual(blockElements, readElements);
        }

        [TestMethod]
        [ExpectedException(typeof(ValidationException))]
        public void TestReadBlockElementsBadIndex()
        {
            var sha256 = new SHA256Managed();

            var blockHash = (UInt256)0;

            var depth0Hash1 = (UInt256)1;
            var depth0Hash2 = (UInt256)2;
            var depth0Hash3 = (UInt256)3;

            var depth1Hash1 = DataCalculatorNew.PairHashes(depth0Hash1, depth0Hash2);
            var depth1Hash2 = DataCalculatorNew.PairHashes(depth0Hash3, depth0Hash3);

            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var blockElements = new List<BlockElement> { 
                new BlockElement(blockHash, index: 0, depth: 0, hash: depth0Hash1),
                new BlockElement(blockHash, index: 1, depth: 0, hash: depth0Hash2),
                new BlockElement(blockHash, index: 3, depth: 0, hash: depth0Hash3),
            };

            var readElements = DataCalculatorNew.ReadBlockElements(blockHash, merkleRoot, blockElements).ToList();

            CollectionAssert.AreEqual(blockElements, readElements);
        }
    }
}
