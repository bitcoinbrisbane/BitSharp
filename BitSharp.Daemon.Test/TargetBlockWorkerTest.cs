using BitSharp.Data;
using BitSharp.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon.Test
{
    [TestClass]
    public class TargetBlockWorkerTest
    {
        [TestMethod]
        public void TestSimpleTargetBlock()
        {
            // initialize data
            var chainedBlock0 = new ChainedBlock(blockHash: 0, previousBlockHash: 9999, height: 0, totalWork: 0);
            var chainedBlock1 = new ChainedBlock(blockHash: 1, previousBlockHash: chainedBlock0.BlockHash, height: 1, totalWork: 1);

            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // initialize the target block watcher
            using (var targetBlockWorker = new TargetBlockWorker(memoryCacheContext, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue))
            {
                // monitor event firing
                var onTargetBlockChangedCount = 0;
                targetBlockWorker.OnTargetBlockChanged += () => onTargetBlockChangedCount++;

                // monitor event firing
                var workNotifyEvent = new AutoResetEvent(false);
                var workStoppedEvent = new AutoResetEvent(false);
                targetBlockWorker.OnNotifyWork += () => workNotifyEvent.Set();
                targetBlockWorker.OnWorkStopped += () => workStoppedEvent.Set();

                // start worker and wait for intial target
                targetBlockWorker.Start();
                workNotifyEvent.WaitOne();
                workStoppedEvent.WaitOne();

                // verify initial state
                Assert.IsNull(targetBlockWorker.TargetBlock);

                // add block 0
                memoryCacheContext.ChainedBlockCache[chainedBlock0.BlockHash] = chainedBlock0;

                // wait for worker
                workNotifyEvent.WaitOne();
                workStoppedEvent.WaitOne();

                // verify block 0
                Assert.AreEqual(chainedBlock0, targetBlockWorker.TargetBlock);
                Assert.AreEqual(1, onTargetBlockChangedCount);

                // add block 1
                memoryCacheContext.ChainedBlockCache[chainedBlock1.BlockHash] = chainedBlock1;

                // wait for worker
                workNotifyEvent.WaitOne();
                workStoppedEvent.WaitOne();

                // verify block 1
                Assert.AreEqual(chainedBlock1, targetBlockWorker.TargetBlock);
                Assert.AreEqual(2, onTargetBlockChangedCount);

                // verify no other work was done
                Assert.IsFalse(workNotifyEvent.WaitOne(0));
                Assert.IsFalse(workStoppedEvent.WaitOne(0));
            }
        }
    }
}
