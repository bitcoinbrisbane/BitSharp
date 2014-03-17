using BitSharp.Blockchain;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using BitSharp.Storage.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon.Test
{
    [TestClass]
    public class ChainingCalculatorTest
    {
        [TestMethod]
        public void TestSimpleChaining()
        {
            // initialize data
            var blockHeaderA0 = new BlockHeader(version: 0, previousBlock: 0, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeaderA1 = new BlockHeader(version: 0, previousBlock: blockHeaderA0.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeaderA2 = new BlockHeader(version: 0, previousBlock: blockHeaderA1.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);

            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // store genesis block
            memoryCacheContext.ChainedBlockCache[blockHeaderA0.Hash] = ChainedBlock.CreateForGenesisBlock(blockHeaderA0);

            // initialize the chaining calculator
            var chainingCalculator = new ChainingCalculator(memoryCacheContext);

            // add block 1
            memoryCacheContext.BlockHeaderCache[blockHeaderA1.Hash] = blockHeaderA1;

            // perform chaining
            chainingCalculator.ChainBlockHeaders();

            // verify block 1
            Assert.AreEqual(2, memoryCacheContext.ChainedBlockCache.Count);
            Assert.IsTrue(memoryCacheContext.ChainedBlockCache.ContainsKey(blockHeaderA1.Hash));
            Assert.AreEqual(
                new ChainedBlock(
                    blockHash: blockHeaderA1.Hash,
                    previousBlockHash: blockHeaderA1.PreviousBlock,
                    height: 1,
                    totalWork: new[] { blockHeaderA0, blockHeaderA1 }.SumBigInteger(x => x.CalculateWork())
                )
                , memoryCacheContext.ChainedBlockCache[blockHeaderA1.Hash]);
            Assert.AreEqual(0, chainingCalculator.UnchainedBlocksByPrevious.Count);

            // add block 2
            memoryCacheContext.BlockHeaderCache[blockHeaderA2.Hash] = blockHeaderA2;

            // perform chaining
            chainingCalculator.ChainBlockHeaders();

            // verify block 2
            Assert.AreEqual(3, memoryCacheContext.ChainedBlockCache.Count);
            Assert.IsTrue(memoryCacheContext.ChainedBlockCache.ContainsKey(blockHeaderA2.Hash));
            Assert.AreEqual(
                new ChainedBlock(
                    blockHash: blockHeaderA2.Hash,
                    previousBlockHash: blockHeaderA2.PreviousBlock,
                    height: 2,
                    totalWork: new[] { blockHeaderA0, blockHeaderA1, blockHeaderA2 }.SumBigInteger(x => x.CalculateWork())
                )
                , memoryCacheContext.ChainedBlockCache[blockHeaderA2.Hash]);
            Assert.AreEqual(0, chainingCalculator.UnchainedBlocksByPrevious.Count);
        }
    }
}
