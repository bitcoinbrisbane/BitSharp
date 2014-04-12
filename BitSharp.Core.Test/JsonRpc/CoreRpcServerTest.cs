using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.JsonRpc;
using BitSharp.Core.Monitor;
using BitSharp.Core.Rules;
using BitSharp.Core.Script;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Ninject;
using NLog;
using System.Collections.Concurrent;

namespace BitSharp.Core.Test.JsonRpc
{
    [TestClass]
    public class CoreRpcServerTest
    {
        [TestMethod]
        public void TestRpcGetBlock()
        {
            using (var simulator = new MainnetSimulator())
            {
                var logger = simulator.Kernel.Get<Logger>();
                using (var rpcServer = new CoreRpcServer(logger, simulator.CoreDaemon))
                {
                    var block9999 = simulator.BlockProvider.GetBlock(9999);

                    simulator.AddBlockRange(0, 9999);
                    simulator.WaitForDaemon();
                    simulator.CloseChainStateBuiler();
                    AssertMethods.AssertDaemonAtBlock(9999, block9999.Hash, simulator.CoreDaemon);

                    //GetBlock call
                }
            }
        }
    }
}
