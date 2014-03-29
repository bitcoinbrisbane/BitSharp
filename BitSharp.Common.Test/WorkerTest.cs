using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common.Test
{
    [TestClass]
    public class WorkerTest
    {
        [TestMethod]
        public void TestSubDispose()
        {
            var callCount = 0;
            Action subDispose = () => callCount++;

            using (var worker = new MockWorker(workAction: () => { }, subDispose: subDispose))
            {
                Assert.AreEqual(0, callCount);
            }
            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        public void TestSubStart()
        {
            var callCount = 0;
            Action subStart = () => callCount++;

            using (var worker = new MockWorker(workAction: () => { }, subStart: subStart))
            {
                Assert.AreEqual(0, callCount);
                worker.Start();
                Assert.AreEqual(1, callCount);
            }
        }


        [TestMethod]
        public void TestSubStop()
        {
            var callCount = 0;
            Action subStop = () => callCount++;

            using (var worker = new MockWorker(workAction: () => { }, subStop: subStop))
            {
                Assert.AreEqual(0, callCount);
                worker.Stop();
                Assert.AreEqual(0, callCount);

                worker.Start();
                Assert.AreEqual(0, callCount);

                worker.Stop();
                Assert.AreEqual(1, callCount);
            }
        }
    }
}
