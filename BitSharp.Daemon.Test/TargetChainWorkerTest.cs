using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using BitSharp.Storage.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
        [TestMethod]
        public void TestSimpleChain()
        {
            // initialize data
            var chainedBlock0 = new ChainedBlock(blockHash: 0, previousBlockHash: 9999, height: 0, totalWork: 0);
            var chainedBlock1 = new ChainedBlock(blockHash: 1, previousBlockHash: chainedBlock0.BlockHash, height: 1, totalWork: 1);
            var chainedBlock2 = new ChainedBlock(blockHash: 2, previousBlockHash: chainedBlock1.BlockHash, height: 2, totalWork: 2);

            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // store genesis block
            memoryCacheContext.ChainedBlockCache[chainedBlock0.BlockHash] = chainedBlock0;

            // mock rules
            var mockRules = new Mock<IBlockchainRules>();
            mockRules.Setup(rules => rules.GenesisChainedBlock).Returns(chainedBlock0);

            // initialize the target chain worker
            using (var targetChainWorker = new TargetChainWorker(mockRules.Object, memoryCacheContext, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue))
            {
                // verify initial state
                Assert.AreEqual(null, targetChainWorker.WinningBlock);
                Assert.AreEqual(null, targetChainWorker.TargetChainedBlocks);

                // monitor event firing
                var workNotifyEvent = new AutoResetEvent(false);
                var workStoppedEvent = new AutoResetEvent(false);
                targetChainWorker.OnNotifyWork += () => workNotifyEvent.Set();
                targetChainWorker.OnWorkStopped += () => workStoppedEvent.Set();

                // start worker and wait for initial chain
                targetChainWorker.Start();
                workStoppedEvent.WaitOne();

                // verify chained to block 0
                Assert.AreEqual(chainedBlock0, targetChainWorker.WinningBlock);
                AssertBlockListEquals(new[] { chainedBlock0 }, targetChainWorker.TargetChainedBlocks.BlockList);

                // add block 1
                memoryCacheContext.ChainedBlockCache[chainedBlock1.BlockHash] = chainedBlock1;

                // wait for target block
                workNotifyEvent.WaitOne();
                Assert.AreEqual(chainedBlock1, targetChainWorker.WinningBlock);

                // wait for target chain
                workStoppedEvent.WaitOne();

                // verify chained to block 1
                AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1 }, targetChainWorker.TargetChainedBlocks.BlockList);

                // verify no other work was done
                Assert.IsFalse(workStoppedEvent.WaitOne(0));

                // add block 2
                memoryCacheContext.ChainedBlockCache[chainedBlock2.BlockHash] = chainedBlock2;

                // wait for target block
                workNotifyEvent.WaitOne();
                Assert.AreEqual(chainedBlock2, targetChainWorker.WinningBlock);

                // wait for target chain
                workStoppedEvent.WaitOne();

                // verify chained to block 2
                AssertBlockListEquals(new[] { chainedBlock0, chainedBlock1, chainedBlock2 }, targetChainWorker.TargetChainedBlocks.BlockList);

                // verify no other work was done
                Assert.IsFalse(workStoppedEvent.WaitOne(0));
            }
        }

        private static void AssertBlockListEquals(ChainedBlock[] chainedBlocks, IImmutableList<ChainedBlock> blockList)
        {
            Assert.AreEqual(chainedBlocks.Length, blockList.Count);
            for (var i = 0; i < blockList.Count; i++)
                Assert.AreEqual(chainedBlocks[i], blockList[i]);
        }
    }
}
