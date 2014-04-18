using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Core.Workers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public class TargetBlockWorkerTest
    {
        [TestMethod]
        public void TestSimpleTargetBlock()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new ConsoleLoggingModule(), new MemoryStorageModule(), new CoreCacheModule());
            var chainedHeaderCache = kernel.Get<ChainedHeaderCache>();

            // initialize data
            var fakeHeaders = new FakeHeaders();
            var chainedHeader0 = new ChainedHeader(fakeHeaders.Genesis(), height: 0, totalWork: 0);
            var chainedHeader1 = new ChainedHeader(fakeHeaders.Next(), height: 1, totalWork: 1);

            // initialize the target block watcher
            using (var targetBlockWorker = kernel.Get<TargetBlockWorker>(new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue))))
            {
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
                chainedHeaderCache[chainedHeader0.Hash] = chainedHeader0;

                // wait for worker
                workNotifyEvent.WaitOne();
                workStoppedEvent.WaitOne();

                // verify block 0
                Assert.AreEqual(chainedHeader0, targetBlockWorker.TargetBlock);
                Assert.AreEqual(1, onTargetBlockChangedCount);

                // add block 1
                chainedHeaderCache[chainedHeader1.Hash] = chainedHeader1;

                // wait for worker
                workNotifyEvent.WaitOne();
                workStoppedEvent.WaitOne();

                // verify block 1
                Assert.AreEqual(chainedHeader1, targetBlockWorker.TargetBlock);
                Assert.AreEqual(2, onTargetBlockChangedCount);

                // verify no other work was done
                Assert.IsFalse(workNotifyEvent.WaitOne(0));
                Assert.IsFalse(workStoppedEvent.WaitOne(0));
            }
        }
    }
}
