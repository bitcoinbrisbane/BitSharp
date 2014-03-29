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
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon.Test
{
    [TestClass]
    public class TargetChainWorkerTest
    {
        //TODO
        // where i have:
        //      wait for worker (chained block addition)
        //      wait for worker (target block changed)
        // these two notifications could potentially complete in one loop, and then the second wait will hang forever

        [TestMethod]
        public void TestSimpleChain()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new MemoryStorageModule(), new CacheModule());

            // initialize data
            var chainedBlock0 = new ChainedBlock(blockHash: 0, previousBlockHash: 9999, height: 0, totalWork: 0);
            var chainedBlock1 = new ChainedBlock(blockHash: 1, previousBlockHash: chainedBlock0.BlockHash, height: 1, totalWork: 1);
            var chainedBlock2 = new ChainedBlock(blockHash: 2, previousBlockHash: chainedBlock1.BlockHash, height: 2, totalWork: 2);

            // mock rules
            var mockRules = new Mock<IBlockchainRules>();
            mockRules.Setup(rules => rules.GenesisChainedBlock).Returns(chainedBlock0);
            kernel.Bind<IBlockchainRules>().ToConstant(mockRules.Object);

            // store genesis block
            var chainedBlockCache = kernel.Get<ChainedBlockCache>();
            chainedBlockCache[chainedBlock0.BlockHash] = chainedBlock0;

            // initialize the target chain worker
            var targetChainWorker = kernel.Get<TargetChainWorker>(new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue)));

            // verify initial state
            Assert.AreEqual(null, targetChainWorker.TargetBlock);
            Assert.AreEqual(null, targetChainWorker.TargetChain);

            // monitor event firing
            var workNotifyEvent = new AutoResetEvent(false);
            var workStoppedEvent = new AutoResetEvent(false);
            var onTargetChainChangedCount = 0;

            targetChainWorker.OnNotifyWork += () => workNotifyEvent.Set();
            targetChainWorker.OnWorkStopped += () => workStoppedEvent.Set();
            targetChainWorker.OnTargetChainChanged += () => onTargetChainChangedCount++;

            // start worker and wait for initial chain
            targetChainWorker.Start();
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 0
            Assert.AreEqual(chainedBlock0, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(1, onTargetChainChangedCount);

            // add block 1
            chainedBlockCache[chainedBlock1.BlockHash] = chainedBlock1;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 1
            Assert.AreEqual(chainedBlock1, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(2, onTargetChainChangedCount);

            // add block 2
            chainedBlockCache[chainedBlock2.BlockHash] = chainedBlock2;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 2
            Assert.AreEqual(chainedBlock2, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(3, onTargetChainChangedCount);

            // verify no other work was done
            Assert.IsFalse(workNotifyEvent.WaitOne(0));
            Assert.IsFalse(workStoppedEvent.WaitOne(0));
        }

        [TestMethod]
        public void TestSimpleChainReverse()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new MemoryStorageModule(), new CacheModule());

            // initialize data
            var chainedBlock0 = new ChainedBlock(blockHash: 0, previousBlockHash: 9999, height: 0, totalWork: 0);
            var chainedBlock1 = new ChainedBlock(blockHash: 1, previousBlockHash: chainedBlock0.BlockHash, height: 1, totalWork: 1);
            var chainedBlock2 = new ChainedBlock(blockHash: 2, previousBlockHash: chainedBlock1.BlockHash, height: 2, totalWork: 2);
            var chainedBlock3 = new ChainedBlock(blockHash: 3, previousBlockHash: chainedBlock2.BlockHash, height: 3, totalWork: 3);
            var chainedBlock4 = new ChainedBlock(blockHash: 4, previousBlockHash: chainedBlock3.BlockHash, height: 4, totalWork: 4);

            // mock rules
            var mockRules = new Mock<IBlockchainRules>();
            mockRules.Setup(rules => rules.GenesisChainedBlock).Returns(chainedBlock0);
            kernel.Bind<IBlockchainRules>().ToConstant(mockRules.Object);

            // store genesis block
            var chainedBlockCache = kernel.Get<ChainedBlockCache>();
            chainedBlockCache[chainedBlock0.BlockHash] = chainedBlock0;

            // initialize the target chain worker
            var targetChainWorker = kernel.Get<TargetChainWorker>(new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue)));

            // verify initial state
            Assert.AreEqual(null, targetChainWorker.TargetBlock);
            Assert.AreEqual(null, targetChainWorker.TargetChain);

            // monitor event firing
            var workNotifyEvent = new AutoResetEvent(false);
            var workStoppedEvent = new AutoResetEvent(false);
            var onTargetChainChangedCount = 0;

            targetChainWorker.OnNotifyWork += () => workNotifyEvent.Set();
            targetChainWorker.OnWorkStopped += () => workStoppedEvent.Set();
            targetChainWorker.OnTargetChainChanged += () => onTargetChainChangedCount++;

            // start worker and wait for initial chain
            targetChainWorker.Start();
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 0
            Assert.AreEqual(chainedBlock0, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(1, onTargetChainChangedCount);

            // add block 4
            chainedBlockCache[chainedBlock4.BlockHash] = chainedBlock4;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify no work done, but the target block should still be updated
            Assert.AreEqual(chainedBlock4, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(1, onTargetChainChangedCount);

            // add block 3
            chainedBlockCache[chainedBlock3.BlockHash] = chainedBlock3;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify no work done
            Assert.AreEqual(chainedBlock4, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(1, onTargetChainChangedCount);

            // add block 2
            chainedBlockCache[chainedBlock2.BlockHash] = chainedBlock2;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify no work done
            Assert.AreEqual(chainedBlock4, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(1, onTargetChainChangedCount);

            // add block 1
            chainedBlockCache[chainedBlock1.BlockHash] = chainedBlock1;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 4
            Assert.AreEqual(chainedBlock4, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2, chainedBlock3, chainedBlock4 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(2, onTargetChainChangedCount);

            // verify no other work was done
            Assert.IsFalse(workNotifyEvent.WaitOne(0));
            Assert.IsFalse(workStoppedEvent.WaitOne(0));
        }

        [TestMethod]
        public void TestTargetChainReorganize()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new MemoryStorageModule(), new CacheModule());

            // initialize data
            var chainedBlock0 = new ChainedBlock(blockHash: 0, previousBlockHash: 9999, height: 0, totalWork: 0);
            var chainedBlock1 = new ChainedBlock(blockHash: 1, previousBlockHash: chainedBlock0.BlockHash, height: 1, totalWork: 1);
            var chainedBlock2 = new ChainedBlock(blockHash: 2, previousBlockHash: chainedBlock1.BlockHash, height: 2, totalWork: 2);

            var chainedBlock3A = new ChainedBlock(blockHash: 3, previousBlockHash: chainedBlock2.BlockHash, height: 3, totalWork: 3);
            var chainedBlock4A = new ChainedBlock(blockHash: 4, previousBlockHash: chainedBlock3A.BlockHash, height: 4, totalWork: 4);
            var chainedBlock5A = new ChainedBlock(blockHash: 5, previousBlockHash: chainedBlock4A.BlockHash, height: 5, totalWork: 5);

            var chainedBlock3B = new ChainedBlock(blockHash: 103, previousBlockHash: chainedBlock2.BlockHash, height: 3, totalWork: 3);
            var chainedBlock4B = new ChainedBlock(blockHash: 104, previousBlockHash: chainedBlock3B.BlockHash, height: 4, totalWork: 10);

            // mock rules
            var mockRules = new Mock<IBlockchainRules>();
            mockRules.Setup(rules => rules.GenesisChainedBlock).Returns(chainedBlock0);
            kernel.Bind<IBlockchainRules>().ToConstant(mockRules.Object);

            // store genesis block
            var chainedBlockCache = kernel.Get<ChainedBlockCache>();
            chainedBlockCache[chainedBlock0.BlockHash] = chainedBlock0;

            // initialize the target chain worker
            var targetChainWorker = kernel.Get<TargetChainWorker>(new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue)));

            // verify initial state
            Assert.AreEqual(null, targetChainWorker.TargetBlock);
            Assert.AreEqual(null, targetChainWorker.TargetChain);

            // monitor event firing
            var workNotifyEvent = new AutoResetEvent(false);
            var workStoppedEvent = new AutoResetEvent(false);
            var onTargetChainChangedCount = 0;

            targetChainWorker.OnNotifyWork += () => workNotifyEvent.Set();
            targetChainWorker.OnWorkStopped += () => workStoppedEvent.Set();
            targetChainWorker.OnTargetChainChanged += () => onTargetChainChangedCount++;

            // start worker and wait for initial chain
            targetChainWorker.Start();
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 0
            Assert.AreEqual(chainedBlock0, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(1, onTargetChainChangedCount);

            // add block 1
            chainedBlockCache[chainedBlock1.BlockHash] = chainedBlock1;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 1
            Assert.AreEqual(chainedBlock1, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(2, onTargetChainChangedCount);

            // add block 2
            chainedBlockCache[chainedBlock2.BlockHash] = chainedBlock2;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 2
            Assert.AreEqual(chainedBlock2, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2 }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(3, onTargetChainChangedCount);

            // add block 3A
            chainedBlockCache[chainedBlock3A.BlockHash] = chainedBlock3A;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 3A
            Assert.AreEqual(chainedBlock3A, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2, chainedBlock3A }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(4, onTargetChainChangedCount);

            // add block 4A
            chainedBlockCache[chainedBlock4A.BlockHash] = chainedBlock4A;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 4A
            Assert.AreEqual(chainedBlock4A, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2, chainedBlock3A, chainedBlock4A }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(5, onTargetChainChangedCount);

            // add block 5A
            chainedBlockCache[chainedBlock5A.BlockHash] = chainedBlock5A;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 5A
            Assert.AreEqual(chainedBlock5A, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2, chainedBlock3A, chainedBlock4A, chainedBlock5A }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(6, onTargetChainChangedCount);

            // add block 3B
            chainedBlockCache[chainedBlock3B.BlockHash] = chainedBlock3B;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify no chaining done
            Assert.AreEqual(chainedBlock5A, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2, chainedBlock3A, chainedBlock4A, chainedBlock5A }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(6, onTargetChainChangedCount);

            // add block 4B
            chainedBlockCache[chainedBlock4B.BlockHash] = chainedBlock4B;

            // wait for worker (chained block addition)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();
            // wait for worker (target block changed)
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify chained to block 4B
            Assert.AreEqual(chainedBlock4B, targetChainWorker.TargetBlock);
            AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2, chainedBlock3B, chainedBlock4B }, targetChainWorker.TargetChain.Blocks);
            Assert.AreEqual(7, onTargetChainChangedCount);

            // verify no other work was done
            Assert.IsFalse(workNotifyEvent.WaitOne(0));
            Assert.IsFalse(workStoppedEvent.WaitOne(0));
        }

        private static void AssertBlockListEquals(ChainedBlock[] expected, IImmutableList<ChainedBlock> actual)
        {
            Assert.AreEqual(expected.Length, actual.Count);
            for (var i = 0; i < actual.Count; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }
    }
}
