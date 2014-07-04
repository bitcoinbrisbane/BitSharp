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
    public class MemoryMerkleWalker : IMerkleWalker
    {
        private List<MerkleTreeNode> merkleTreeNodes;

        public IEnumerable<MerkleTreeNode> StreamNodes()
        {
            return this.merkleTreeNodes;
        }
    }

    [TestClass]
    public class DataCalculatorNewTest
    {
        [TestMethod]
        public void TestPruneElements()
        {
            var sha256 = new SHA256Managed();

            var merkleWalker = new MemoryMerkleWalker();

            var node1 = new MerkleTreeNode(index: 0, depth: 0, hash: 1);
            var node2 = new MerkleTreeNode(index: 1, depth: 0, hash: 2);
            var node3 = new MerkleTreeNode(index: 2, depth: 0, hash: 3);
            var node4 = new MerkleTreeNode(index: 3, depth: 0, hash: 4);

            var depth1Hash1 = DataCalculatorNew.PairHashes(node1.Hash, node2.Hash);
            var depth1Hash2 = DataCalculatorNew.PairHashes(node3.Hash, node4.Hash);
            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var nodes = new List<MerkleTreeNode> { node1, node2, node3, node4 };

            var actualNodes = DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot, nodes).ToList();

            CollectionAssert.AreEqual(nodes, actualNodes);
        }

        [TestMethod]
        public void TestReadMerkleTreeNodes()
        {
            var sha256 = new SHA256Managed();

            var node1 = new MerkleTreeNode(index: 0, depth: 0, hash: 1);
            var node2 = new MerkleTreeNode(index: 1, depth: 0, hash: 2);
            var node3 = new MerkleTreeNode(index: 2, depth: 0, hash: 3);
            var node4 = new MerkleTreeNode(index: 3, depth: 0, hash: 4);

            var depth1Hash1 = DataCalculatorNew.PairHashes(node1.Hash, node2.Hash);
            var depth1Hash2 = DataCalculatorNew.PairHashes(node3.Hash, node4.Hash);
            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var nodes = new List<MerkleTreeNode> { node1, node2, node3, node4 };

            var actualNodes = DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot, nodes).ToList();

            CollectionAssert.AreEqual(nodes, actualNodes);
        }

        [TestMethod]
        public void TestReadMerkleTreeNodesOddPower()
        {
            var sha256 = new SHA256Managed();

            var depth0Hash1 = (UInt256)1;
            var depth0Hash2 = (UInt256)2;
            var depth0Hash3 = (UInt256)3;

            var depth1Hash1 = DataCalculatorNew.PairHashes(depth0Hash1, depth0Hash2);
            var depth1Hash2 = DataCalculatorNew.PairHashes(depth0Hash3, depth0Hash3);

            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var nodes = new List<MerkleTreeNode> { 
                new MerkleTreeNode(index: 0, depth: 0, hash: depth0Hash1),
                new MerkleTreeNode(index: 1, depth: 0, hash: depth0Hash2),
                new MerkleTreeNode(index: 2, depth: 0, hash: depth0Hash3),
            };

            var actualNodes = DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot, nodes).ToList();

            CollectionAssert.AreEqual(nodes, actualNodes);
        }

        [TestMethod]
        public void TestReadMerkleTreeNodesPruned()
        {
            var sha256 = new SHA256Managed();

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

            var nodes = new List<MerkleTreeNode> { 
                new MerkleTreeNode(index: 0, depth: 0, hash: depth0Hash1),
                new MerkleTreeNode(index: 1, depth: 0, hash: depth0Hash2),
                new MerkleTreeNode(index: 2, depth: 0, hash: depth0Hash3),
                new MerkleTreeNode(index: 3, depth: 0, hash: depth0Hash4),
                        new MerkleTreeNode(index: 4, depth: 2, hash: depth2Hash2),
                new MerkleTreeNode(index: 8, depth: 0, hash: depth0Hash9),
                new MerkleTreeNode(index: 9, depth: 0, hash: depth0Hash10),
                    new MerkleTreeNode(index: 10, depth: 1, hash: depth1Hash6),
                new MerkleTreeNode(index: 12, depth: 0, hash: depth0Hash13),
                new MerkleTreeNode(index: 13, depth: 0, hash: depth0Hash14),
                    new MerkleTreeNode(index: 14, depth: 1, hash: depth1Hash8),
            };

            var actualNodes = DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot, nodes).ToList();

            CollectionAssert.AreEqual(nodes, actualNodes);
        }

        [TestMethod]
        public void TestReadMerkleTreeNodesPerformance()
        {
            var sha256 = new SHA256Managed();

            var count = 10.THOUSAND();
            var nodes = new List<MerkleTreeNode>(count);
            for (var i = 0; i < count; i++)
            {
                nodes.Add(new MerkleTreeNode(i, depth: 0, hash: i));
            }

            var merkleRoot = new MethodTimer().Time(() =>
                DataCalculator.CalculateMerkleRoot(nodes.Select(x => x.Hash).ToImmutableList()));

            var actualNodes = new MethodTimer().Time(() =>
                DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot, nodes).ToList());

            CollectionAssert.AreEqual(nodes, actualNodes);
        }

        [TestMethod]
        [ExpectedException(typeof(ValidationException))]
        public void TestReadMerkleTreeNodesBadDepth()
        {
            var sha256 = new SHA256Managed();

            var depth0Hash1 = (UInt256)1;
            var depth0Hash2 = (UInt256)2;
            var depth0Hash3 = (UInt256)3;

            var depth1Hash1 = DataCalculatorNew.PairHashes(depth0Hash1, depth0Hash2);
            var depth1Hash2 = DataCalculatorNew.PairHashes(depth0Hash3, depth0Hash3);

            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var nodes = new List<MerkleTreeNode> { 
                new MerkleTreeNode(index: 0, depth: 0, hash: depth0Hash1),
                new MerkleTreeNode(index: 1, depth: 0, hash: depth0Hash2),
                new MerkleTreeNode(index: 2, depth: 1, hash: depth0Hash3),
            };

            var actualNodes = DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot, nodes).ToList();

            CollectionAssert.AreEqual(nodes, actualNodes);
        }

        [TestMethod]
        [ExpectedException(typeof(ValidationException))]
        public void TestReadMerkleTreeNodesBadIndex()
        {
            var sha256 = new SHA256Managed();

            var depth0Hash1 = (UInt256)1;
            var depth0Hash2 = (UInt256)2;
            var depth0Hash3 = (UInt256)3;

            var depth1Hash1 = DataCalculatorNew.PairHashes(depth0Hash1, depth0Hash2);
            var depth1Hash2 = DataCalculatorNew.PairHashes(depth0Hash3, depth0Hash3);

            var merkleRoot = DataCalculatorNew.PairHashes(depth1Hash1, depth1Hash2);

            var nodes = new List<MerkleTreeNode> { 
                new MerkleTreeNode(index: 0, depth: 0, hash: depth0Hash1),
                new MerkleTreeNode(index: 1, depth: 0, hash: depth0Hash2),
                new MerkleTreeNode(index: 3, depth: 0, hash: depth0Hash3),
            };

            var actualNodes = DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot, nodes).ToList();

            CollectionAssert.AreEqual(nodes, actualNodes);
        }
    }
}
