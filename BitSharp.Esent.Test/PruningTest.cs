using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using BitSharp.Core.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent.Test
{
    [TestClass]
    public class PruningTest
    {
        [TestCleanup]
        public void Cleanup()
        {
            EsentTests.CleanBaseDirectory();
        }

        [TestMethod]
        public void TestPrune()
        {
            var baseDirectory = EsentTests.PrepareBaseDirectory();

            var txCount = 100;
            var transactions = Enumerable.Range(0, txCount).Select(x => RandomData.RandomTransaction()).ToImmutableArray();
            var blockHeader = RandomData.RandomBlockHeader().With(MerkleRoot: DataCalculator.CalculateMerkleRoot(transactions));
            var block = new Block(blockHeader, transactions);

            var expectedFinalDepth = (int)Math.Ceiling(Math.Log(txCount, 2));
            var expectedFinalElement = new BlockElement(index: 0, depth: expectedFinalDepth, hash: blockHeader.MerkleRoot, pruned: true);

            var pruneOrderSource = Enumerable.Range(0, txCount).ToList();
            var pruneOrder = new List<int>(txCount);

            var random = new Random();
            while (pruneOrderSource.Count > 0)
            {
                var randomIndex = random.Next(pruneOrderSource.Count);

                pruneOrder.Add(pruneOrderSource[randomIndex]);
                pruneOrderSource.RemoveAt(randomIndex);
            }

            using (var blockStorage = new BlockStorageNew(baseDirectory))
            {
                blockStorage.AddBlock(block);
                var blockTxes = blockStorage.ReadBlock(block.Hash, block.Header.MerkleRoot).ToList();

                new MethodTimer().Time(() =>
                {
                    using (var merkleWalker = blockStorage.OpenWalker(block.Hash))
                    {
                        foreach (var pruneIndex in pruneOrder)
                        {
                            merkleWalker.BeginTransaction();
                            DataCalculatorNew.PruneNode(merkleWalker, pruneIndex);
                            merkleWalker.CommitTransaction();

                            blockStorage.ReadBlockElements(block.Hash, block.Header.MerkleRoot).ToList();
                        }

                        var finalBlockElements = blockStorage.ReadBlockElements(block.Hash, block.Header.MerkleRoot).ToList();

                        Assert.AreEqual(1, finalBlockElements.Count);
                        Assert.AreEqual(expectedFinalElement, finalBlockElements[0]);
                    }
                });
            }
        }
    }
}
