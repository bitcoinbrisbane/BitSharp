using BitSharp.Common;
using BitSharp.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    [TestClass]
    public class BlockchainWalkerTest
    {
        private ChainedBlock chainedBlock0;
        private ChainedBlock chainedBlock1;

        private ChainedBlock chainedBlockA2;
        private ChainedBlock chainedBlockA3;
        private ChainedBlock chainedBlockA4;

        private ChainedBlock chainedBlockB2;
        private ChainedBlock chainedBlockB3;
        private ChainedBlock chainedBlockB4;

        private ChainedBlock chainedBlockX0;
        private ChainedBlock chainedBlockX1;

        private ImmutableDictionary<UInt256, ChainedBlock> chainedBlocks;
        private Func<UInt256, ChainedBlock> getChainedBlock;

        [TestInitialize]
        public void Init()
        {
            this.chainedBlock0 = new ChainedBlock(0, 1111, 0, 0);
            this.chainedBlock1 = new ChainedBlock(1, 0, 1, 0);

            this.chainedBlockA2 = new ChainedBlock(2, 1, 2, 0);
            this.chainedBlockA3 = new ChainedBlock(3, 2, 3, 0);
            this.chainedBlockA4 = new ChainedBlock(4, 3, 4, 0);

            this.chainedBlockB2 = new ChainedBlock(102, 1, 2, 0);
            this.chainedBlockB3 = new ChainedBlock(103, 102, 3, 0);
            this.chainedBlockB4 = new ChainedBlock(104, 103, 4, 0);

            this.chainedBlockX0 = new ChainedBlock(9000, 9999, 0, 0);
            this.chainedBlockX1 = new ChainedBlock(9001, 9000, 1, 0);

            this.chainedBlocks = ImmutableDictionary.CreateRange(
                new[] { chainedBlock0, chainedBlock1, chainedBlockA2, chainedBlockA3, chainedBlockA4, chainedBlockB2, chainedBlockB3, chainedBlockB4, chainedBlockX0, chainedBlockX1 }
                .Select(x => new KeyValuePair<UInt256, ChainedBlock>(x.BlockHash, x)));

            this.getChainedBlock = blockHash => this.chainedBlocks[blockHash];
        }

        [TestMethod]
        public void TestSameBlock()
        {
            var walker = new BlockchainWalker();

            var path = walker.GetBlockchainPath(this.chainedBlockA2, this.chainedBlockA2, this.getChainedBlock);

            Assert.AreEqual(this.chainedBlockA2, path.FromBlock);
            Assert.AreEqual(this.chainedBlockA2, path.ToBlock);
            Assert.AreEqual(this.chainedBlockA2, path.LastCommonBlock);

            Assert.AreEqual(0, path.RewindBlocks.Count);
            Assert.AreEqual(0, path.AdvanceBlocks.Count);
        }

        [TestMethod]
        public void TestAdvanceOnly()
        {
            var walker = new BlockchainWalker();

            var path = walker.GetBlockchainPath(this.chainedBlockA2, this.chainedBlockA4, this.getChainedBlock);

            Assert.AreEqual(this.chainedBlockA2, path.FromBlock);
            Assert.AreEqual(this.chainedBlockA4, path.ToBlock);
            Assert.AreEqual(this.chainedBlockA2, path.LastCommonBlock);

            Assert.AreEqual(0, path.RewindBlocks.Count);

            Assert.AreEqual(2, path.AdvanceBlocks.Count);
            Assert.AreEqual(this.chainedBlockA3, path.AdvanceBlocks[0]);
            Assert.AreEqual(this.chainedBlockA4, path.AdvanceBlocks[1]);
        }

        [TestMethod]
        public void TestRewindOnly()
        {
            var walker = new BlockchainWalker();

            var path = walker.GetBlockchainPath(this.chainedBlockA4, this.chainedBlockA2, this.getChainedBlock);

            Assert.AreEqual(this.chainedBlockA4, path.FromBlock);
            Assert.AreEqual(this.chainedBlockA2, path.ToBlock);
            Assert.AreEqual(this.chainedBlockA2, path.LastCommonBlock);

            Assert.AreEqual(2, path.RewindBlocks.Count);
            Assert.AreEqual(this.chainedBlockA4, path.RewindBlocks[0]);
            Assert.AreEqual(this.chainedBlockA3, path.RewindBlocks[1]);

            Assert.AreEqual(0, path.AdvanceBlocks.Count);
        }

        [TestMethod]
        public void TestRewindAndAdvanceFromHigher()
        {
            var walker = new BlockchainWalker();

            var path = walker.GetBlockchainPath(this.chainedBlockA4, this.chainedBlockB3, this.getChainedBlock);

            Assert.AreEqual(this.chainedBlockA4, path.FromBlock);
            Assert.AreEqual(this.chainedBlockB3, path.ToBlock);
            Assert.AreEqual(this.chainedBlock1, path.LastCommonBlock);

            Assert.AreEqual(3, path.RewindBlocks.Count);
            Assert.AreEqual(this.chainedBlockA4, path.RewindBlocks[0]);
            Assert.AreEqual(this.chainedBlockA3, path.RewindBlocks[1]);
            Assert.AreEqual(this.chainedBlockA2, path.RewindBlocks[2]);

            Assert.AreEqual(2, path.AdvanceBlocks.Count);
            Assert.AreEqual(this.chainedBlockB2, path.AdvanceBlocks[0]);
            Assert.AreEqual(this.chainedBlockB3, path.AdvanceBlocks[1]);
        }

        [TestMethod]
        public void TestRewindAndAdvanceToHigher()
        {
            var walker = new BlockchainWalker();

            var path = walker.GetBlockchainPath(this.chainedBlockA3, this.chainedBlockB4, this.getChainedBlock);

            Assert.AreEqual(this.chainedBlockA3, path.FromBlock);
            Assert.AreEqual(this.chainedBlockB4, path.ToBlock);
            Assert.AreEqual(this.chainedBlock1, path.LastCommonBlock);

            Assert.AreEqual(2, path.RewindBlocks.Count);
            Assert.AreEqual(this.chainedBlockA3, path.RewindBlocks[0]);
            Assert.AreEqual(this.chainedBlockA2, path.RewindBlocks[1]);

            Assert.AreEqual(3, path.AdvanceBlocks.Count);
            Assert.AreEqual(this.chainedBlockB2, path.AdvanceBlocks[0]);
            Assert.AreEqual(this.chainedBlockB3, path.AdvanceBlocks[1]);
            Assert.AreEqual(this.chainedBlockB4, path.AdvanceBlocks[2]);
        }

        [TestMethod]
        public void TestRewindAndAdvanceSameHeight()
        {
            var walker = new BlockchainWalker();

            var path = walker.GetBlockchainPath(this.chainedBlockA4, this.chainedBlockB4, this.getChainedBlock);

            Assert.AreEqual(this.chainedBlockA4, path.FromBlock);
            Assert.AreEqual(this.chainedBlockB4, path.ToBlock);
            Assert.AreEqual(this.chainedBlock1, path.LastCommonBlock);

            Assert.AreEqual(3, path.RewindBlocks.Count);
            Assert.AreEqual(this.chainedBlockA4, path.RewindBlocks[0]);
            Assert.AreEqual(this.chainedBlockA3, path.RewindBlocks[1]);
            Assert.AreEqual(this.chainedBlockA2, path.RewindBlocks[2]);

            Assert.AreEqual(3, path.AdvanceBlocks.Count);
            Assert.AreEqual(this.chainedBlockB2, path.AdvanceBlocks[0]);
            Assert.AreEqual(this.chainedBlockB3, path.AdvanceBlocks[1]);
            Assert.AreEqual(this.chainedBlockB4, path.AdvanceBlocks[2]);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestChainMismatch()
        {
            var walker = new BlockchainWalker();

            walker.GetBlockchainPath(this.chainedBlockA4, this.chainedBlockX0, this.getChainedBlock);
        }
    }
}
