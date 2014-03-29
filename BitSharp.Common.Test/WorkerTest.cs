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
            var wasCalled = false;
            Action subDispose = () => wasCalled = true;

            using (var worker = new MockWorker(workAction: () => { }, subDispose: subDispose))
            {
                Assert.IsFalse(wasCalled);
            }
            Assert.IsTrue(wasCalled);
        }
    }
}
