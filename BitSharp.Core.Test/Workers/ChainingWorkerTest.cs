using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Core.Workers;
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

namespace BitSharp.Core.Test.Workers
{
    [TestClass]
    public class ChainingWorkerTest
    {
        [TestMethod]
        public void TestSimpleChaining()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new ConsoleLoggingModule(), new MemoryStorageModule(), new CoreCacheModule(), new RulesModule(RulesEnum.MainNet));
            var blockHeaderCache = kernel.Get<BlockHeaderCache>();
            var chainedHeaderCache = kernel.Get<ChainedHeaderCache>();

            // mock rules
            var mockRules = Mock.Of<IBlockchainRules>();

            // initialize data
            var blockHeader0 = new BlockHeader(version: 0, previousBlock: 0, merkleRoot: 0, time: 0, bits: 0x1D00FFFF, nonce: 0);
            var blockHeader1 = new BlockHeader(version: 0, previousBlock: blockHeader0.Hash, merkleRoot: 0, time: 0, bits: 0x1D00FFFF, nonce: 0);
            var blockHeader2 = new BlockHeader(version: 0, previousBlock: blockHeader1.Hash, merkleRoot: 0, time: 0, bits: 0x1D00FFFF, nonce: 0);

            // store genesis block
            blockHeaderCache[blockHeader0.Hash] = blockHeader0;
            chainedHeaderCache[blockHeader0.Hash] = ChainedHeader.CreateForGenesisBlock(blockHeader0);

            // initialize the chaining worker
            using (var chainingWorker = kernel.Get<ChainingWorker>(new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue))))
            {
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
                Assert.AreEqual(2, chainedHeaderCache.Count);
                Assert.IsTrue(chainedHeaderCache.ContainsKey(blockHeader1.Hash));
                Assert.AreEqual(
                    new ChainedHeader(
                        blockHeader: blockHeader1,
                        height: 1,
                        totalWork: new[] { blockHeader0, blockHeader1 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , chainedHeaderCache[blockHeader1.Hash]);
                Assert.AreEqual(0, chainingWorker.UnchainedByPrevious.Count);

                // add block 2
                blockHeaderCache[blockHeader2.Hash] = blockHeader2;

                // wait for chaining
                workStoppedEvent.WaitOne();

                // verify block 2
                Assert.AreEqual(3, chainedHeaderCache.Count);
                Assert.IsTrue(chainedHeaderCache.ContainsKey(blockHeader2.Hash));
                Assert.AreEqual(
                    new ChainedHeader(
                        blockHeader: blockHeader2,
                        height: 2,
                        totalWork: new[] { blockHeader0, blockHeader1, blockHeader2 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , chainedHeaderCache[blockHeader2.Hash]);
                Assert.AreEqual(0, chainingWorker.UnchainedByPrevious.Count);

                // verify no other work was done
                Assert.IsFalse(workStoppedEvent.WaitOne(0));
            }
        }

        [TestMethod]
        public void TestReverseChaining()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new ConsoleLoggingModule(), new MemoryStorageModule(), new CoreCacheModule(), new RulesModule(RulesEnum.MainNet));
            var blockHeaderCache = kernel.Get<BlockHeaderCache>();
            var chainedHeaderCache = kernel.Get<ChainedHeaderCache>();

            // mock rules
            var mockRules = Mock.Of<IBlockchainRules>();

            // initialize data
            var blockHeader0 = new BlockHeader(version: 0, previousBlock: 0, merkleRoot: 0, time: 0, bits: 0x1D00FFFF, nonce: 0);
            var blockHeader1 = new BlockHeader(version: 0, previousBlock: blockHeader0.Hash, merkleRoot: 0, time: 0, bits: 0x1D00FFFF, nonce: 0);
            var blockHeader2 = new BlockHeader(version: 0, previousBlock: blockHeader1.Hash, merkleRoot: 0, time: 0, bits: 0x1D00FFFF, nonce: 0);
            var blockHeader3 = new BlockHeader(version: 0, previousBlock: blockHeader2.Hash, merkleRoot: 0, time: 0, bits: 0x1D00FFFF, nonce: 0);
            var blockHeader4 = new BlockHeader(version: 0, previousBlock: blockHeader3.Hash, merkleRoot: 0, time: 0, bits: 0x1D00FFFF, nonce: 0);

            // store genesis block
            blockHeaderCache[blockHeader0.Hash] = blockHeader0;
            chainedHeaderCache[blockHeader0.Hash] = ChainedHeader.CreateForGenesisBlock(blockHeader0);

            // initialize the chaining worker
            using (var chainingWorker = kernel.Get<ChainingWorker>(new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue))))
            {
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
                Assert.AreEqual(1, chainedHeaderCache.Count);
                Assert.AreEqual(1, chainingWorker.UnchainedByPrevious.Count);
                AssertSingleUnchainedHeaderByPrevious(blockHeader4, chainingWorker.UnchainedByPrevious);

                // add block 3
                blockHeaderCache[blockHeader3.Hash] = blockHeader3;

                // wait for chaining
                workStoppedEvent.WaitOne();

                // verify nothing chained
                Assert.AreEqual(1, chainedHeaderCache.Count);
                Assert.AreEqual(2, chainingWorker.UnchainedByPrevious.Count);
                AssertSingleUnchainedHeaderByPrevious(blockHeader3, chainingWorker.UnchainedByPrevious);
                AssertSingleUnchainedHeaderByPrevious(blockHeader4, chainingWorker.UnchainedByPrevious);

                // add block 2
                blockHeaderCache[blockHeader2.Hash] = blockHeader2;

                // wait for chaining
                workStoppedEvent.WaitOne();

                // verify nothing chained
                Assert.AreEqual(1, chainedHeaderCache.Count);
                Assert.AreEqual(3, chainingWorker.UnchainedByPrevious.Count);
                Assert.IsTrue(chainingWorker.UnchainedByPrevious.ContainsKey(blockHeader2.PreviousBlock));
                AssertSingleUnchainedHeaderByPrevious(blockHeader2, chainingWorker.UnchainedByPrevious);
                AssertSingleUnchainedHeaderByPrevious(blockHeader3, chainingWorker.UnchainedByPrevious);
                AssertSingleUnchainedHeaderByPrevious(blockHeader4, chainingWorker.UnchainedByPrevious);

                // add block 1
                blockHeaderCache[blockHeader1.Hash] = blockHeader1;

                // wait for chaining
                workStoppedEvent.WaitOne();

                // verify all blocks chained
                Assert.AreEqual(5, chainedHeaderCache.Count);
                Assert.AreEqual(0, chainingWorker.UnchainedByPrevious.Count);

                // verify block 1
                Assert.IsTrue(chainedHeaderCache.ContainsKey(blockHeader1.Hash));
                Assert.AreEqual(
                    new ChainedHeader(
                        blockHeader: blockHeader1,
                        height: 1,
                        totalWork: new[] { blockHeader0, blockHeader1 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , chainedHeaderCache[blockHeader1.Hash]);

                // verify block 2
                Assert.IsTrue(chainedHeaderCache.ContainsKey(blockHeader2.Hash));
                Assert.AreEqual(
                    new ChainedHeader(
                        blockHeader: blockHeader2,
                        height: 2,
                        totalWork: new[] { blockHeader0, blockHeader1, blockHeader2 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , chainedHeaderCache[blockHeader2.Hash]);

                // verify block 3
                Assert.IsTrue(chainedHeaderCache.ContainsKey(blockHeader3.Hash));
                Assert.AreEqual(
                    new ChainedHeader(
                        blockHeader: blockHeader3,
                        height: 3,
                        totalWork: new[] { blockHeader0, blockHeader1, blockHeader2, blockHeader3 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , chainedHeaderCache[blockHeader3.Hash]);

                // verify block 4
                Assert.IsTrue(chainedHeaderCache.ContainsKey(blockHeader4.Hash));
                Assert.AreEqual(
                    new ChainedHeader(
                        blockHeader: blockHeader4,
                        height: 4,
                        totalWork: new[] { blockHeader0, blockHeader1, blockHeader2, blockHeader3, blockHeader4 }.SumBigInteger(x => x.CalculateWork())
                    )
                    , chainedHeaderCache[blockHeader4.Hash]);

                // verify no other work was done
                Assert.IsFalse(workStoppedEvent.WaitOne(0));
            }
        }

        private static void AssertSingleUnchainedHeaderByPrevious(BlockHeader blockHeader, IReadOnlyDictionary<UInt256, IReadOnlyDictionary<UInt256, BlockHeader>> unchainByPrevious)
        {
            Assert.IsTrue(unchainByPrevious.ContainsKey(blockHeader.PreviousBlock));
            Assert.AreEqual(1, unchainByPrevious[blockHeader.PreviousBlock].Count);
            Assert.IsTrue(unchainByPrevious[blockHeader.PreviousBlock].ContainsKey(blockHeader.Hash));
        }
    }
}
