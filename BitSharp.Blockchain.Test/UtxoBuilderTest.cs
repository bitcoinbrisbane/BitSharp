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
        public void TestSimpleFullSpend()
        {
            // prepare an unspent transaction
            var txHash = new UInt256(100);
            var txOutputIndex = 0U;
            var txUnspentOutputs = new ImmutableBitArray(1, false);
            var unspentTx = new UnspentTx(txHash, 1, OutputState.Unspent);

            // mock a parent utxo containing the unspent transaction
            var mockParentUtxo = new Mock<Utxo>();
            mockParentUtxo.Setup(utxo => utxo.UnspentTransactions()).Returns(new[] { unspentTx });

            // initialize memory utxo builder storage
            var memoryUtxoBuilderStorage = new MemoryUtxoBuilderStorage(mockParentUtxo.Object);

            // mock a cache context
            var mockCacheContext = new Mock<ICacheContext>();
            mockCacheContext.Setup(cc => cc.ToUtxoBuilder(mockParentUtxo.Object)).Returns(memoryUtxoBuilderStorage);

            // initialize utxo builder
            var utxoBuilder = new UtxoBuilder(mockCacheContext.Object, mockParentUtxo.Object);

            // create an input to spend the unspent transaction
            var input = new TxInput(new TxOutputKey(txHash, txOutputIndex), ImmutableList.Create<byte>(), 0);

            // spend the input
            utxoBuilder.Spend(input);

            // verify utxo storage
            Assert.IsFalse(memoryUtxoBuilderStorage.Storage.ContainsKey(txHash));
        }

        [TestMethod]
        [ExpectedException(typeof(ValidationException))]
        public void TestDoubleSpend()
        {
            // prepare an unspent transaction
            var txHash = new UInt256(100);
            var txOutputIndex = 0U;
            var txUnspentOutputs = new ImmutableBitArray(1, false);
            var unspentTx = new UnspentTx(txHash, 1, OutputState.Unspent);

            // mock a parent utxo containing the unspent transaction
            var mockParentUtxo = new Mock<Utxo>();
            mockParentUtxo.Setup(utxo => utxo.UnspentTransactions()).Returns(new[] { unspentTx });

            // initialize memory utxo builder storage
            var memoryUtxoBuilderStorage = new MemoryUtxoBuilderStorage(mockParentUtxo.Object);

            // mock a cache context
            var mockCacheContext = new Mock<ICacheContext>();
            mockCacheContext.Setup(cc => cc.ToUtxoBuilder(mockParentUtxo.Object)).Returns(memoryUtxoBuilderStorage);

            // initialize utxo builder
            var utxoBuilder = new UtxoBuilder(mockCacheContext.Object, mockParentUtxo.Object);

            // create an input to spend the unspent transaction
            var input = new TxInput(new TxOutputKey(txHash, txOutputIndex), ImmutableList.Create<byte>(), 0);

            // spend the input
            utxoBuilder.Spend(input);

            // verify utxo storage
            Assert.IsFalse(memoryUtxoBuilderStorage.Storage.ContainsKey(txHash));

            // attempt to spend the input again
            utxoBuilder.Spend(input);

            // validation exception should be thrown
        }
    }
}
