using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
            using (var chainStateMonitor = new ChainStateMonitor(LogManager.CreateNullLogger()))
            using (chainStateMonitor.Start())
            {
                try
                {
                    // mock IChainStateVisitor
                    var visitor = new Mock<IChainStateVisitor>();

                    // keep track of BeginBlock calls
                    var wasVisited = false;
                    var visitEvent = new AutoResetEvent(false);
                    visitor.Setup(x => x.BeginBlock(It.IsAny<ChainedHeader>())).Callback(() => visitEvent.Set());

                    // subscribe visitor
                    using (var subscription = chainStateMonitor.Subscribe(visitor.Object))
                    {
                        // verify no BeginBlock calls
                        wasVisited = visitEvent.WaitOne(10);
                        Assert.IsFalse(wasVisited);

                        // call BeginBlock
                        chainStateMonitor.BeginBlock(null);

                        // verify BeginBlock call
                        wasVisited = visitEvent.WaitOne(100);
                        Assert.IsTrue(wasVisited);
                    }

                    // call BeginBlock, after unsubscribe
                    chainStateMonitor.BeginBlock(null);

                    // verify no additional BeginBlock call
                    wasVisited = visitEvent.WaitOne(10);
                    Assert.IsFalse(wasVisited);
                }
                finally
                {
                    // wait for monitor
                    chainStateMonitor.CompleteAdding();
                    chainStateMonitor.WaitToComplete();
                }
            }
        }
    }
}
