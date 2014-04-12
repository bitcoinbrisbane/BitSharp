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

namespace BitSharp.Core.Test.JsonRpc
{
    [TestClass]
    public class CoreRpcServerTest
    {
        [TestMethod]
        public void Test()
        {
            var sha256 = new SHA256Managed();

            //var publicKey =
            //    "04ea1feff861b51fe3f5f8a3b12d0f4712db80e919548a80839fc47c6a21e66d957e9c5d8cd108c7a2d2324bad71f9904ac0ae7336507d785b17a2c115e427a32f"
            //    .HexToByteArray();

            var publicKey =
                "04f9804cfb86fb17441a6562b07c4ee8f012bdb2da5be022032e4b87100350ccc7c0f4d47078b06c9d22b0ec10bdce4c590e0d01aed618987a6caa8c94d74ee6dc"
                .HexToByteArray();

            var outputScript1 = new PayToPublicKeyBuilder().CreateOutput(publicKey);
            var outputScript1Hash = new UInt256(sha256.ComputeHash(outputScript1));
            var outputScript2 = new PayToPublicKeyHashBuilder().CreateOutputFromPublicKey(publicKey);
            var outputScript2Hash = new UInt256(sha256.ComputeHash(outputScript2));

            var outputScriptHashes = new HashSet<UInt256>();
            for (var i = 0; i < 1.MILLION(); i++)
                outputScriptHashes.Add(i);

            outputScriptHashes.Add(outputScript1Hash);
            outputScriptHashes.Add(outputScript2Hash);

            var txMonitor = new Mock<ITransactionMonitor>();

            var mintedTxOutputs = new List<TxOutput>();
            var spentTxOutputs = new List<TxOutput>();

            txMonitor.Setup(x => x.MintTxOutput(It.IsAny<TxOutput>())).Callback<TxOutput>(
                txOutput =>
                {
                    if (outputScriptHashes.Contains(new UInt256(sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()))))
                    {
                        Debug.WriteLine("+{0} BTC".Format2((decimal)txOutput.Value / (decimal)(100.MILLION())));
                        mintedTxOutputs.Add(txOutput);
                    }
                });
            var outputScript = new PayToPublicKeyBuilder().CreateOutput(publicKey);
            var outputScriptHash = new UInt256(sha256.ComputeHash(outputScript));

            txMonitor.Setup(x => x.SpendTxOutput(It.IsAny<TxOutput>())).Callback<TxOutput>(
                txOutput =>
                {
                    if (outputScriptHashes.Contains(new UInt256(sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()))))
                    {
                        Debug.WriteLine("-{0} BTC".Format2((decimal)txOutput.Value / (decimal)(100.MILLION())));
                        spentTxOutputs.Add(txOutput);
                    }
                });

            using (var simulator = new MainnetSimulator())
            {
                simulator.CoreDaemon.RegistorMonitor(txMonitor.Object);

                var block9999 = simulator.BlockProvider.GetBlock(9999);

                simulator.AddBlockRange(0, 9999);
                simulator.WaitForDaemon();
                simulator.CloseChainStateBuiler();
                AssertMethods.AssertDaemonAtBlock(9999, block9999.Hash, simulator.CoreDaemon);

                Debug.WriteLine(mintedTxOutputs.Count);
                Debug.WriteLine(spentTxOutputs.Count);

                Debug.WriteLine(mintedTxOutputs.Sum(x => (decimal)x.Value) / (decimal)(100.MILLION()));
                Debug.WriteLine(spentTxOutputs.Sum(x => (decimal)x.Value) / (decimal)(100.MILLION()));

                Debug.WriteLine(
                    (mintedTxOutputs.Sum(x => (decimal)x.Value)
                    - spentTxOutputs.Sum(x => (decimal)x.Value))
                    / (decimal)(100.MILLION()));


                Assert.AreEqual(32.92M,
                    (mintedTxOutputs.Sum(x => (decimal)x.Value)
                    - spentTxOutputs.Sum(x => (decimal)x.Value))
                    / (decimal)(100.MILLION()));


                //TODO make rpc call to get balanace and verify

                //var utxo = simulator.CoreDaemon.ChainState.Utxo;

                //var scriptUnspentValue = 0UL;
                //foreach (var keyPair in utxo.GetUnspentOutputs())
                //{
                //    var unspentOutput = keyPair.Value;
                //    var unspentOutputScriptHash = new UInt256(sha256.ComputeHash(unspentOutput.ScriptPublicKey.ToArray()));

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
