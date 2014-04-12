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
using Newtonsoft.Json;

namespace BitSharp.Core.Test.JsonRpc
{
    [TestClass]
    public class CoreRpcServerTest
    {
        [TestMethod]
        [Ignore]
        public void TestRpcGetBlock()
        {
            using (var simulator = new MainnetSimulator())
            {
                var logger = simulator.Kernel.Get<Logger>();
                using (var rpcServer = new CoreRpcServer(logger, simulator.CoreDaemon))
                {
                    rpcServer.StartListening();

                    var block9 = simulator.BlockProvider.GetBlock(9);

                    simulator.AddBlockRange(0, 9);
                    simulator.WaitForDaemon();
                    simulator.CloseChainStateBuiler();
                    AssertMethods.AssertDaemonAtBlock(9, block9.Hash, simulator.CoreDaemon);

                    var jsonRequest = JsonConvert.SerializeObject(new JsonCall { method = "getblock" });

                    Assert.Fail("TODO: Need HTTP JSON-RPC functionality.");
                }
            }
        }

        private sealed class JsonCall
        {
            public string method { get; set; }
        }
    }
}
