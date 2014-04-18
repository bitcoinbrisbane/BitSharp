using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test.Monitor
{
    [TestClass]
    public class ChainStateMonitorTest
    {
        [TestMethod]
        public void TestUnsubscribe()
        {
            // initialize ChainStateMonitor
            var chainStateMonitor = new ChainStateMonitor();

            // mock IChainStateVisitor
            var visitor = new Mock<IChainStateVisitor>();

            // keep track of BeginBlock calls
            var visitCount = 0;
            visitor.Setup(x => x.BeginBlock(It.IsAny<ChainedHeader>())).Callback(() => visitCount++);

            // subscribe visitor
            using (var subscription = chainStateMonitor.Subscribe(visitor.Object))
            {
                // verify no BeginBlock calls
                Assert.AreEqual(0, visitCount);

                // call BeginBlock
                chainStateMonitor.BeginBlock(null);

                // verify BeginBlock call
                Assert.AreEqual(1, visitCount);
            }

            // call BeginBlock, after unsubscribe
            chainStateMonitor.BeginBlock(null);

            // verify no additional BeginBlock call
            Assert.AreEqual(1, visitCount);
        }
    }
}
