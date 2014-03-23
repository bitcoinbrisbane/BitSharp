using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using BitSharp.Storage.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain.Test
{
    [TestClass]
    public class UtxoBuilderTest
    {
        [TestMethod]
        public void TestSimpleSpend()
        {
            var mockCacheContext = new Mock<ICacheContext>();
            var mockUtxoBuilderStorage = new Mock<IUtxoBuilderStorage>();
            var mockParentUtxo = Mock.Of<Utxo>();

            mockCacheContext.Setup(cc => cc.ToUtxoBuilder(mockParentUtxo)).Returns(mockUtxoBuilderStorage.Object);

            var txHash = new UInt256(100);
            var txOutputIndex = 0U;
            var txUnspentOutputs = new ImmutableBitArray(1, false);

            mockUtxoBuilderStorage.Setup(utxo => utxo.ContainsKey(txHash)).Returns(true);
            mockUtxoBuilderStorage.Setup(utxo => utxo[txHash]).Returns(new UnspentTx(txHash, 1, OutputState.Unspent));

            var utxoBuilder = new UtxoBuilder(mockCacheContext.Object, mockParentUtxo);

            var input = new TxInput(new TxOutputKey(txHash, txOutputIndex), ImmutableList.Create<byte>(), 0);
            utxoBuilder.Spend(input);
        }

        [TestMethod]
        public void TestDoubleSpend()
        {
        }
    }
}
