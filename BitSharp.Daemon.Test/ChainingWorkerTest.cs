using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
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
    public class ChainingWorkerTest
    {
        [TestMethod]
        public void TestSimpleChaining()
        {
            // mock rules
            var mockRules = Mock.Of<IBlockchainRules>();

            // initialize data
            var blockHeader0 = new BlockHeader(version: 0, previousBlock: 0, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader1 = new BlockHeader(version: 0, previousBlock: blockHeader0.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader2 = new BlockHeader(version: 0, previousBlock: blockHeader1.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);

            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // store genesis block
            memoryCacheContext.BlockHeaderCache[blockHeader0.Hash] = blockHeader0;
            memoryCacheContext.ChainedBlockCache[blockHeader0.Hash] = ChainedBlock.CreateForGenesisBlock(blockHeader0);

            // initialize the chaining worker
            using (var chainingWorker = new ChainingWorker(mockRules, memoryCacheContext, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue))
            {
                // monitor event firing
                var workStoppedEvent = new AutoResetEvent(false);
                chainingWorker.OnWorkStopped += () => workStoppedEvent.Set();

                // start and wait for initial chaining
                chainingWorker.Start();
                workStoppedEvent.WaitOne();

                // add block 1
                memoryCacheContext.BlockHeaderCache[blockHeader1.Hash] = blockHeader1;

                // wait for chaining
                workStoppedEvent.WaitOne();

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
                Assert.AreEqual(0, chainingWorker.UnchainByPrevious.Count);

                // add block 2
                memoryCacheContext.BlockHeaderCache[blockHeader2.Hash] = blockHeader2;

                // wait for chaining
                workStoppedEvent.WaitOne();

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
                Assert.AreEqual(0, chainingWorker.UnchainByPrevious.Count);

                // verify no other work was done
                Assert.IsFalse(workStoppedEvent.WaitOne(0));
            }
        }

        [TestMethod]
        public void TestReverseChaining()
        {
            // mock rules
            var mockRules = Mock.Of<IBlockchainRules>();

            // initialize data
            var blockHeader0 = new BlockHeader(version: 0, previousBlock: 0, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader1 = new BlockHeader(version: 0, previousBlock: blockHeader0.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader2 = new BlockHeader(version: 0, previousBlock: blockHeader1.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader3 = new BlockHeader(version: 0, previousBlock: blockHeader2.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader4 = new BlockHeader(version: 0, previousBlock: blockHeader3.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);

            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // store genesis block
            memoryCacheContext.BlockHeaderCache[blockHeader0.Hash] = blockHeader0;
            memoryCacheContext.ChainedBlockCache[blockHeader0.Hash] = ChainedBlock.CreateForGenesisBlock(blockHeader0);

            // initialize the chaining worker
            using (var chainingWorker = new ChainingWorker(mockRules, memoryCacheContext, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue))
            {
                // monitor event firing
                var workStoppedEvent = new AutoResetEvent(false);
                chainingWorker.OnWorkStopped += () => workStoppedEvent.Set();

                // start and wait for initial chaining
                chainingWorker.Start();
                workStoppedEvent.WaitOne();

                // add block 4
                memoryCacheContext.BlockHeaderCache[blockHeader4.Hash] = blockHeader4;

                // wait for chaining
                workStoppedEvent.WaitOne();

                // verify nothing chained
                Assert.AreEqual(1, memoryCacheContext.ChainedBlockCache.Count);
                Assert.AreEqual(1, chainingWorker.UnchainByPrevious.Count);
                AssertSingleUnchainedBlockByPrevious(blockHeader4, chainingWorker.UnchainByPrevious);

                // add block 3
                memoryCacheContext.BlockHeaderCache[blockHeader3.Hash] = blockHeader3;

                // wait for chaining
                workStoppedEvent.WaitOne();

                // verify nothing chained
                Assert.AreEqual(1, memoryCacheContext.ChainedBlockCache.Count);
                Assert.AreEqual(2, chainingWorker.UnchainByPrevious.Count);
                AssertSingleUnchainedBlockByPrevious(blockHeader3, chainingWorker.UnchainByPrevious);
                AssertSingleUnchainedBlockByPrevious(blockHeader4, chainingWorker.UnchainByPrevious);

                // add block 2
                memoryCacheContext.BlockHeaderCache[blockHeader2.Hash] = blockHeader2;

                // wait for chaining
                workStoppedEvent.WaitOne();

                // verify nothing chained
                Assert.AreEqual(1, memoryCacheContext.ChainedBlockCache.Count);
                Assert.AreEqual(3, chainingWorker.UnchainByPrevious.Count);
                Assert.IsTrue(chainingWorker.UnchainByPrevious.ContainsKey(blockHeader2.PreviousBlock));
                AssertSingleUnchainedBlockByPrevious(blockHeader2, chainingWorker.UnchainByPrevious);
                AssertSingleUnchainedBlockByPrevious(blockHeader3, chainingWorker.UnchainByPrevious);
                AssertSingleUnchainedBlockByPrevious(blockHeader4, chainingWorker.UnchainByPrevious);

                // add block 1
                memoryCacheContext.BlockHeaderCache[blockHeader1.Hash] = blockHeader1;

                // wait for chaining
                workStoppedEvent.WaitOne();

                // verify all blocks chained
                Assert.AreEqual(5, memoryCacheContext.ChainedBlockCache.Count);
                Assert.AreEqual(0, chainingWorker.UnchainByPrevious.Count);

                // verify block 1
                Assert.IsTrue(memoryCacheContext.ChainedBlockCache.ContainsKey(blockHeader1.Hash));
                Assert.AreEqual(
                    new ChainedBlock(
                        blockHash: blockHeader1.Hash,
                        previousBlockHash: blockHeader1.PreviousBlock,
                        height: 1,
                        totalWork: new[] { blockHeader0, blockHeader1 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , memoryCacheContext.ChainedBlockCache[blockHeader1.Hash]);

                // verify block 2
                Assert.IsTrue(memoryCacheContext.ChainedBlockCache.ContainsKey(blockHeader2.Hash));
                Assert.AreEqual(
                    new ChainedBlock(
                        blockHash: blockHeader2.Hash,
                        previousBlockHash: blockHeader2.PreviousBlock,
                        height: 2,
                        totalWork: new[] { blockHeader0, blockHeader1, blockHeader2 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , memoryCacheContext.ChainedBlockCache[blockHeader2.Hash]);

                // verify block 3
                Assert.IsTrue(memoryCacheContext.ChainedBlockCache.ContainsKey(blockHeader3.Hash));
                Assert.AreEqual(
                    new ChainedBlock(
                        blockHash: blockHeader3.Hash,
                        previousBlockHash: blockHeader3.PreviousBlock,
                        height: 3,
                        totalWork: new[] { blockHeader0, blockHeader1, blockHeader2, blockHeader3 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , memoryCacheContext.ChainedBlockCache[blockHeader3.Hash]);

                // verify block 4
                Assert.IsTrue(memoryCacheContext.ChainedBlockCache.ContainsKey(blockHeader4.Hash));
                Assert.AreEqual(
                    new ChainedBlock(
                        blockHash: blockHeader4.Hash,
                        previousBlockHash: blockHeader4.PreviousBlock,
                        height: 4,
                        totalWork: new[] { blockHeader0, blockHeader1, blockHeader2, blockHeader3, blockHeader4 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , memoryCacheContext.ChainedBlockCache[blockHeader4.Hash]);

                // verify no other work was done
                Assert.IsFalse(workStoppedEvent.WaitOne(0));
            }
        }

        private static void AssertSingleUnchainedBlockByPrevious(BlockHeader blockHeader, IReadOnlyDictionary<UInt256, IReadOnlyDictionary<UInt256, BlockHeader>> unchainByPrevious)
        {
            Assert.IsTrue(unchainByPrevious.ContainsKey(blockHeader.PreviousBlock));
            Assert.AreEqual(1, unchainByPrevious[blockHeader.PreviousBlock].Count);
            Assert.IsTrue(unchainByPrevious[blockHeader.PreviousBlock].ContainsKey(blockHeader.Hash));
        }
    }
}
