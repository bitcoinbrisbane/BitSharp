using BitSharp.Data;
using BitSharp.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ninject;
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
            // prepare test kernel
            var kernel = new StandardKernel(new MemoryStorageModule());
            var chainedBlockCache = kernel.Get<ChainedBlockCache>();
            
                // initialize data
            var chainedBlock0 = new ChainedBlock(blockHash: 0, previousBlockHash: 9999, height: 0, totalWork: 0);
            var chainedBlock1 = new ChainedBlock(blockHash: 1, previousBlockHash: chainedBlock0.BlockHash, height: 1, totalWork: 1);

            // initialize the target block watcher
            var targetBlockWorker = kernel.Get<TargetBlockWorker>();

            // monitor event firing
            var workNotifyEvent = new AutoResetEvent(false);
            var workStoppedEvent = new AutoResetEvent(false);
            var onTargetBlockChangedCount = 0;

            targetBlockWorker.OnNotifyWork += () => workNotifyEvent.Set();
            targetBlockWorker.OnWorkStopped += () => workStoppedEvent.Set();
            targetBlockWorker.OnTargetBlockChanged += () => onTargetBlockChangedCount++;

            // start worker and wait for intial target
            targetBlockWorker.Start();
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify initial state
            Assert.IsNull(targetBlockWorker.TargetBlock);

            // add block 0
            chainedBlockCache[chainedBlock0.BlockHash] = chainedBlock0;

            // wait for worker
            workNotifyEvent.WaitOne();
            workStoppedEvent.WaitOne();

            // verify block 0
            Assert.AreEqual(chainedBlock0, targetBlockWorker.TargetBlock);
            Assert.AreEqual(1, onTargetBlockChangedCount);

            // add block 1
            chainedBlockCache[chainedBlock1.BlockHash] = chainedBlock1;

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
