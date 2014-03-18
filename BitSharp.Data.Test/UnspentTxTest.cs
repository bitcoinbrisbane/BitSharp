using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public class UnspentTxTest
    {
        [TestMethod]
        public void TestUnspentTxEquality()
        {
            var randomUnspentTx = RandomData.RandomUnspentTx();

            var sameUnspentTx = new UnspentTx
            (
                txHash: randomUnspentTx.TxHash,
                unspentOutputs: randomUnspentTx.UnspentOutputs
            );

            var differentUnspentTxTxHash = new UnspentTx
            (
                txHash: ~randomUnspentTx.TxHash,
                unspentOutputs: randomUnspentTx.UnspentOutputs
            );

            var differentUnspentTxUnpsentOutputs = new UnspentTx
            (
                txHash: ~randomUnspentTx.TxHash,
                unspentOutputs: randomUnspentTx.UnspentOutputs
            );

            Assert.IsTrue(randomUnspentTx.Equals(sameUnspentTx));
            Assert.IsTrue(randomUnspentTx == sameUnspentTx);
            Assert.IsFalse(randomUnspentTx != sameUnspentTx);

            Assert.IsFalse(randomUnspentTx.Equals(differentUnspentTxTxHash));
            Assert.IsFalse(randomUnspentTx == differentUnspentTxTxHash);
            Assert.IsTrue(randomUnspentTx != differentUnspentTxTxHash);

            Assert.IsFalse(randomUnspentTx.Equals(differentUnspentTxUnpsentOutputs));
            Assert.IsFalse(randomUnspentTx == differentUnspentTxUnpsentOutputs);
            Assert.IsTrue(randomUnspentTx != differentUnspentTxUnpsentOutputs);
        }
    }
}
