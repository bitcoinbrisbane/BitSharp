using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using System.Collections;
using BitSharp.Core.Domain;
using BitSharp.Core.Builders;

namespace BitSharp.Core.Test
{
    [TestClass]
    public class BlockReplayerTest
    {
        [TestMethod]
        public void TestReplayBlock()
        {
            using (var simulator = new MainnetSimulator())
            {
                //simulator.CoreDaemon.SubscribeChainStateVisitor(walletMonitor);

                //var block9999 = simulator.BlockProvider.GetBlock(9999);
                simulator.AddBlockRange(0, 9999);

                simulator.WaitForUpdate();

                var chain = simulator.CoreDaemon.CurrentChain;

                for (var blockHeight = 0; blockHeight <= chain.Height; blockHeight++)
                {
                    var expectedTransactions = simulator.BlockProvider.GetBlock(blockHeight).Transactions;

                    var actualTransactions = simulator.CoreDaemon.ReplayBlock(chain.Blocks[blockHeight].Hash).ToList();

                    CollectionAssert.AreEqual(expectedTransactions, actualTransactions, new TxHashComparer(), "Transactions differ at block {0:#,##0}".Format2(blockHeight));
                }
            }
        }

        private sealed class TxHashComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                var txX = (Transaction)x;
                var txY = (TxWithPrevOutputs)y;
                return txX.Hash.CompareTo(txY.Transaction.Hash);
            }
        }
    }
}
