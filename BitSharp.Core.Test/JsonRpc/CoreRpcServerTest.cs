using BitSharp.Common.ExtensionMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test.JsonRpc
{
    [TestClass]
    public class CoreRpcServerTest
    {
        [TestMethod]
        public void Test()
        {
            using (var simulator = new MainnetSimulator())
            {
                var block9999 = simulator.BlockProvider.GetBlock(9999);

                simulator.AddBlockRange(0, 9999);
                simulator.WaitForDaemon();
                simulator.CloseChainStateBuiler();

                AssertMethods.AssertDaemonAtBlock(9999, block9999.Hash, simulator.CoreDaemon);

                Assert.Fail("unfinished test");
            }
        }
    }
}
