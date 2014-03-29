using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Ninject;
using Ninject.Parameters;
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
            // prepare test kernel
            var kernel = new StandardKernel(new MemoryStorageModule(), new CacheModule(), new RulesModule(RulesEnum.MainNet));
            var blockHeaderCache = kernel.Get<BlockHeaderCache>();
            var chainedBlockCache = kernel.Get<ChainedBlockCache>();

            // mock rules
            var mockRules = Mock.Of<IBlockchainRules>();

            // initialize data
            var blockHeader0 = new BlockHeader(version: 0, previousBlock: 0, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader1 = new BlockHeader(version: 0, previousBlock: blockHeader0.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader2 = new BlockHeader(version: 0, previousBlock: blockHeader1.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);

            // store genesis block
            blockHeaderCache[blockHeader0.Hash] = blockHeader0;
            chainedBlockCache[blockHeader0.Hash] = ChainedBlock.CreateForGenesisBlock(blockHeader0);

            // initialize the chaining worker
            var chainingWorker = kernel.Get<ChainingWorker>(new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue)));

            // monitor event firing
            var workStoppedEvent = new AutoResetEvent(false);
            chainingWorker.OnWorkStopped += () => workStoppedEvent.Set();

            // start and wait for initial chaining
            chainingWorker.Start();
            workStoppedEvent.WaitOne();

            // add block 1
            blockHeaderCache[blockHeader1.Hash] = blockHeader1;

            // wait for chaining
            workStoppedEvent.WaitOne();

            // verify block 1
            Assert.AreEqual(2, chainedBlockCache.Count);
            Assert.IsTrue(chainedBlockCache.ContainsKey(blockHeader1.Hash));
            Assert.AreEqual(
                new ChainedBlock(
                    blockHash: blockHeader1.Hash,
                    previousBlockHash: blockHeader1.PreviousBlock,
                    height: 1,
                    totalWork: new[] { blockHeader0, blockHeader1 }.SumBigInteger(x => x.CalculateWork())
                )
                , chainedBlockCache[blockHeader1.Hash]);
            Assert.AreEqual(0, chainingWorker.UnchainByPrevious.Count);

            // add block 2
            blockHeaderCache[blockHeader2.Hash] = blockHeader2;

            // wait for chaining
            workStoppedEvent.WaitOne();

            // verify block 2
            Assert.AreEqual(3, chainedBlockCache.Count);
            Assert.IsTrue(chainedBlockCache.ContainsKey(blockHeader2.Hash));
            Assert.AreEqual(
                new ChainedBlock(
                    blockHash: blockHeader2.Hash,
                    previousBlockHash: blockHeader2.PreviousBlock,
                    height: 2,
                    totalWork: new[] { blockHeader0, blockHeader1, blockHeader2 }.SumBigInteger(x => x.CalculateWork())
                )
                , chainedBlockCache[blockHeader2.Hash]);
            Assert.AreEqual(0, chainingWorker.UnchainByPrevious.Count);

            // verify no other work was done
            Assert.IsFalse(workStoppedEvent.WaitOne(0));
        }

        [TestMethod]
        public void TestReverseChaining()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new MemoryStorageModule(), new CacheModule(), new RulesModule(RulesEnum.MainNet));
            var blockHeaderCache = kernel.Get<BlockHeaderCache>();
            var chainedBlockCache = kernel.Get<ChainedBlockCache>();

            // mock rules
            var mockRules = Mock.Of<IBlockchainRules>();

            // initialize data
            var blockHeader0 = new BlockHeader(version: 0, previousBlock: 0, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader1 = new BlockHeader(version: 0, previousBlock: blockHeader0.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader2 = new BlockHeader(version: 0, previousBlock: blockHeader1.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader3 = new BlockHeader(version: 0, previousBlock: blockHeader2.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);
            var blockHeader4 = new BlockHeader(version: 0, previousBlock: blockHeader3.Hash, merkleRoot: 0, time: 0, bits: 486604799, nonce: 0);

            // store genesis block
            blockHeaderCache[blockHeader0.Hash] = blockHeader0;
            chainedBlockCache[blockHeader0.Hash] = ChainedBlock.CreateForGenesisBlock(blockHeader0);

            // initialize the chaining worker
            var chainingWorker = kernel.Get<ChainingWorker>(new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue)));

            // monitor event firing
            var workStoppedEvent = new AutoResetEvent(false);
            chainingWorker.OnWorkStopped += () => workStoppedEvent.Set();

            // start and wait for initial chaining
            chainingWorker.Start();
            workStoppedEvent.WaitOne();

            // add block 4
            blockHeaderCache[blockHeader4.Hash] = blockHeader4;

            // wait for chaining
            workStoppedEvent.WaitOne();

            // verify nothing chained
            Assert.AreEqual(1, chainedBlockCache.Count);
            Assert.AreEqual(1, chainingWorker.UnchainByPrevious.Count);
            AssertSingleUnchainedBlockByPrevious(blockHeader4, chainingWorker.UnchainByPrevious);

            // add block 3
            blockHeaderCache[blockHeader3.Hash] = blockHeader3;

            // wait for chaining
            workStoppedEvent.WaitOne();

            // verify nothing chained
            Assert.AreEqual(1, chainedBlockCache.Count);
            Assert.AreEqual(2, chainingWorker.UnchainByPrevious.Count);
            AssertSingleUnchainedBlockByPrevious(blockHeader3, chainingWorker.UnchainByPrevious);
            AssertSingleUnchainedBlockByPrevious(blockHeader4, chainingWorker.UnchainByPrevious);

            // add block 2
            blockHeaderCache[blockHeader2.Hash] = blockHeader2;

            // wait for chaining
            workStoppedEvent.WaitOne();

            // verify nothing chained
            Assert.AreEqual(1, chainedBlockCache.Count);
            Assert.AreEqual(3, chainingWorker.UnchainByPrevious.Count);
            Assert.IsTrue(chainingWorker.UnchainByPrevious.ContainsKey(blockHeader2.PreviousBlock));
            AssertSingleUnchainedBlockByPrevious(blockHeader2, chainingWorker.UnchainByPrevious);
            AssertSingleUnchainedBlockByPrevious(blockHeader3, chainingWorker.UnchainByPrevious);
            AssertSingleUnchainedBlockByPrevious(blockHeader4, chainingWorker.UnchainByPrevious);

            // add block 1
            blockHeaderCache[blockHeader1.Hash] = blockHeader1;

            // wait for chaining
            workStoppedEvent.WaitOne();

            // verify all blocks chained
            Assert.AreEqual(5, chainedBlockCache.Count);
            Assert.AreEqual(0, chainingWorker.UnchainByPrevious.Count);

            // verify block 1
            Assert.IsTrue(chainedBlockCache.ContainsKey(blockHeader1.Hash));
            Assert.AreEqual(
                new ChainedBlock(
                    blockHash: blockHeader1.Hash,
                    previousBlockHash: blockHeader1.PreviousBlock,
                    height: 1,
                    totalWork: new[] { blockHeader0, blockHeader1 }.SumBigInteger(x => x.CalculateWork())
                )
                , chainedBlockCache[blockHeader1.Hash]);

            // verify block 2
            Assert.IsTrue(chainedBlockCache.ContainsKey(blockHeader2.Hash));
            Assert.AreEqual(
                new ChainedBlock(
                    blockHash: blockHeader2.Hash,
                    previousBlockHash: blockHeader2.PreviousBlock,
                    height: 2,
                    totalWork: new[] { blockHeader0, blockHeader1, blockHeader2 }.SumBigInteger(x => x.CalculateWork())
                )
                , chainedBlockCache[blockHeader2.Hash]);

            // verify block 3
            Assert.IsTrue(chainedBlockCache.ContainsKey(blockHeader3.Hash));
            Assert.AreEqual(
                new ChainedBlock(
                    blockHash: blockHeader3.Hash,
                    previousBlockHash: blockHeader3.PreviousBlock,
                    height: 3,
                    totalWork: new[] { blockHeader0, blockHeader1, blockHeader2, blockHeader3 }.SumBigInteger(x => x.CalculateWork())
                )
                , chainedBlockCache[blockHeader3.Hash]);

            // verify block 4
            Assert.IsTrue(chainedBlockCache.ContainsKey(blockHeader4.Hash));
            Assert.AreEqual(
                new ChainedBlock(
                    blockHash: blockHeader4.Hash,
                    previousBlockHash: blockHeader4.PreviousBlock,
                    height: 4,
                    totalWork: new[] { blockHeader0, blockHeader1, blockHeader2, blockHeader3, blockHeader4 }.SumBigInteger(x => x.CalculateWork())
                )
                , chainedBlockCache[blockHeader4.Hash]);

            // verify no other work was done
            Assert.IsFalse(workStoppedEvent.WaitOne(0));
        }

        private static void AssertSingleUnchainedBlockByPrevious(BlockHeader blockHeader, IReadOnlyDictionary<UInt256, IReadOnlyDictionary<UInt256, BlockHeader>> unchainByPrevious)
        {
            Assert.IsTrue(unchainByPrevious.ContainsKey(blockHeader.PreviousBlock));
            Assert.AreEqual(1, unchainByPrevious[blockHeader.PreviousBlock].Count);
            Assert.IsTrue(unchainByPrevious[blockHeader.PreviousBlock].ContainsKey(blockHeader.Hash));
        }
    }
}
