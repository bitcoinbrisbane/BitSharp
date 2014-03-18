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
            var blockHeader0 = new BlockHeader(version: 0, previousBlock: 0, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader1 = new BlockHeader(version: 0, previousBlock: blockHeader0.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader2 = new BlockHeader(version: 0, previousBlock: blockHeader1.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);

            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // store genesis block
            memoryCacheContext.ChainedBlockCache[blockHeader0.Hash] = ChainedBlock.CreateForGenesisBlock(blockHeader0);

            // initialize the chaining calculator
            using (var chainingCalculator = new ChainingCalculator(memoryCacheContext))
            {
                // monitor event firing
                var eventCount = 0;
                chainingCalculator.OnQueued += () => eventCount++;

                // add block 1
                memoryCacheContext.BlockHeaderCache[blockHeader1.Hash] = blockHeader1;
                Assert.AreEqual(1, eventCount);

                // perform chaining
                chainingCalculator.ChainBlockHeaders();

                // verify block 1
                Assert.AreEqual(2, memoryCacheContext.ChainedBlockCache.Count);
                Assert.IsTrue(memoryCacheContext.ChainedBlockCache.ContainsKey(blockHeader1.Hash));
                Assert.AreEqual(
                    new ChainedBlock(
                        blockHash: blockHeader1.Hash,
                        previousBlockHash: blockHeader1.PreviousBlock,
                        height: 1,
                        totalWork: new[] { blockHeader0, blockHeader1 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , memoryCacheContext.ChainedBlockCache[blockHeader1.Hash]);
                Assert.AreEqual(0, chainingCalculator.UnchainedBlocksByPrevious.Count);

                // add block 2
                memoryCacheContext.BlockHeaderCache[blockHeader2.Hash] = blockHeader2;
                Assert.AreEqual(2, eventCount);

                // perform chaining
                chainingCalculator.ChainBlockHeaders();

                // verify block 2
                Assert.AreEqual(3, memoryCacheContext.ChainedBlockCache.Count);
                Assert.IsTrue(memoryCacheContext.ChainedBlockCache.ContainsKey(blockHeader2.Hash));
                Assert.AreEqual(
                    new ChainedBlock(
                        blockHash: blockHeader2.Hash,
                        previousBlockHash: blockHeader2.PreviousBlock,
                        height: 2,
                        totalWork: new[] { blockHeader0, blockHeader1, blockHeader2 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , memoryCacheContext.ChainedBlockCache[blockHeader2.Hash]);
                Assert.AreEqual(0, chainingCalculator.UnchainedBlocksByPrevious.Count);
            }
        }
    }
}
