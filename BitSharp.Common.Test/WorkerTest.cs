using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common.Test
{
    [TestClass]
    public class WorkerTest
    {
        [TestMethod]
        public void TestSubDispose()
        {
            // prepare subDispose call tracking
            var callCount = 0;
            Action subDispose = () => callCount++;

            // initialize worker
            using (var worker = new MockWorker(workAction: () => { }, subDispose: subDispose))
            {
                // verify subDispose has not been called
                Assert.AreEqual(0, callCount);
            }

            // verify subDispose has been called after end of using
            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        public void TestSubStart()
        {
            // prepare subStart call tracking
            var callCount = 0;
            Action subStart = () => callCount++;

            // initialize worker
            using (var worker = new MockWorker(workAction: () => { }, subStart: subStart))
            {
                // verify subStart has not been called
                Assert.AreEqual(0, callCount);

                // start the worker
                worker.Start();

                // verify subStart has been called
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        public void TestSubStop()
        {
            // prepare subStart call tracking
            var callCount = 0;
            Action subStop = () => callCount++;

            // initialize worker
            using (var worker = new MockWorker(workAction: () => { }, subStop: subStop))
            {
                // verify subStop has not been called
                Assert.AreEqual(0, callCount);

                // stop the worker before it has been started
                worker.Stop();

                // verify subStop has not been called
                Assert.AreEqual(0, callCount);

                // start the worker
                worker.Start();

                // verify subStop has not been called
                Assert.AreEqual(0, callCount);

                // stop the worker
                worker.Stop();

                // verify subStop has been called
                Assert.AreEqual(1, callCount);
            }
        }

        [TestMethod]
        public void TestNotifyWork()
        {
            // prepare workAction call tracking
            var callEvent = new AutoResetEvent(false);
            Action workAction = () => callEvent.Set();

            // initialize worker
            using (var worker = new MockWorker(workAction))
            {
                // verify workAction has not been called
                var wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);

                // start worker
                worker.Start();

                // verify workAction has not been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);

                // notify worker
                worker.NotifyWork();

                // verify workAction has been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsTrue(wasCalled);

                // stop worker
                worker.Stop();

                // verify workAction has not been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);
            }
        }

        [TestMethod]
        public void TestStartNotified()
        {
            // prepare workAction call tracking
            var callEvent = new AutoResetEvent(false);
            Action workAction = () => callEvent.Set();

            // initialize worker
            using (var worker = new MockWorker(workAction))
            {
                // verify workAction has not been called
                var wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);

                // notify worker before it has been started
                worker.NotifyWork();

                // verify workAction has not been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);

                // start worker, it has already been notified
                worker.Start();

                // verify workAction has been called
                wasCalled = callEvent.WaitOne(1000);
                Assert.IsTrue(wasCalled);

                // stop worker
                worker.Stop();

                // verify workAction has not been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);
            }
        }

        [TestMethod]
        public void TestRestart()
        {
            // prepare workAction call tracking
            var callEvent = new AutoResetEvent(false);
            Action workAction = () => callEvent.Set();

            // initialize worker
            using (var worker = new MockWorker(workAction))
            {
                // start worker
                worker.Start();

                // verify workAction has not been called
                var wasCalled = callEvent.WaitOne(1000);
                Assert.IsFalse(wasCalled);

                // notify worker
                worker.NotifyWork();

                // verify workAction has been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsTrue(wasCalled);

                // stop worker
                worker.Stop();

                // verify workAction has not been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);

                // start worker again
                worker.Start();

                // notify worker
                worker.NotifyWork();

                // verify workAction has been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsTrue(wasCalled);
            }
        }

        [TestMethod]
        public void TestRestartNotified()
        {
            // prepare workAction call tracking
            var callEvent = new AutoResetEvent(false);
            Action workAction = () => callEvent.Set();

            // initialize worker
            using (var worker = new MockWorker(workAction))
            {
                // start worker
                worker.Start();

                // verify workAction has not been called
                var wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);

                // notify worker
                worker.NotifyWork();

                // verify workAction has been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsTrue(wasCalled);

                // stop worker
                worker.Stop();

                // notify worker again
                worker.NotifyWork();

                // verify workAction has not been called
                wasCalled = callEvent.WaitOne(10);
                Assert.IsFalse(wasCalled);

                // start worker again, it has already been notified
                worker.Start();

                // verify workAction has been called
                wasCalled = callEvent.WaitOne(1000);
                Assert.IsTrue(wasCalled);
            }
        }
    }
}
