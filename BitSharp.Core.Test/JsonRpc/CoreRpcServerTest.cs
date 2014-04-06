using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.JsonRpc;
using BitSharp.Core.Rules;
using BitSharp.Core.Script;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var publicKey =
                "04ea1feff861b51fe3f5f8a3b12d0f4712db80e919548a80839fc47c6a21e66d957e9c5d8cd108c7a2d2324bad71f9904ac0ae7336507d785b17a2c115e427a32f"
                .HexToByteArray();
            var outputScript = new PayToPublicKeyBuilder().CreateOutput(publicKey);
            var outputScriptHash = new UInt256(Crypto.DoubleSHA256(outputScript));

            using (var simulator = new MainnetSimulator())
            {
                //TODO register script to monitor balance

                var block9999 = simulator.BlockProvider.GetBlock(9999);

                simulator.AddBlockRange(0, 9999);
                simulator.WaitForDaemon();
                simulator.CloseChainStateBuiler();
                AssertMethods.AssertDaemonAtBlock(9999, block9999.Hash, simulator.CoreDaemon);

                //TODO make rpc call to get balanace and verify

                //var utxo = simulator.CoreDaemon.ChainState.Utxo;

                //var scriptUnspentValue = 0UL;
                //foreach (var keyPair in utxo.GetUnspentOutputs())
                //{
                //    var unspentOutput = keyPair.Value;
                //    var unspentOutputScriptHash = new UInt256(Crypto.DoubleSHA256(unspentOutput.ScriptPublicKey.ToArray()));

                //    Debug.WriteLine(scriptHash);
                //    Debug.WriteLine(unspentOutputScriptHash);
                //    if (scriptHash == unspentOutputScriptHash)
                //    {
                //        scriptUnspentValue += unspentOutput.Value;
                //    }
                //}

                //Assert.AreEqual(6100000000UL, scriptUnspentValue);

                Assert.Fail("unfinished test");
            }
        }
    }
}
