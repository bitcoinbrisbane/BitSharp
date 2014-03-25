using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
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
            // prepare block
            var chainedBlock = new ChainedBlock(blockHash: 1, previousBlockHash: 0, height: 0, totalWork: 0);

            // prepare an unspent transaction
            var txHash = new UInt256(100);
            var unspentTx = new UnspentTx(txHash, 3, OutputState.Unspent);

            // mock a parent utxo containing the unspent transaction
            var mockParentUtxoStorage = new Mock<IUtxoStorage>();
            mockParentUtxoStorage.Setup(utxo => utxo.UnspentTransactions()).Returns(new[] { unspentTx });
            var parentUtxo = new Utxo(mockParentUtxoStorage.Object);

            // initialize memory utxo builder storage
            var memoryUtxoBuilderStorage = new MemoryUtxoBuilderStorage(mockParentUtxoStorage.Object);

            // mock a cache context
            var mockCacheContext = new Mock<ICacheContext>();
            mockCacheContext.Setup(cc => cc.ToUtxoBuilder(mockParentUtxoStorage.Object)).Returns(memoryUtxoBuilderStorage);

            // initialize utxo builder
            var utxoBuilder = new UtxoBuilder(mockCacheContext.Object, parentUtxo);

            // create an input to spend the unspent transaction's first output
            var input1 = new TxInput(new TxOutputKey(txHash, txOutputIndex: 0), ImmutableArray.Create<byte>(), 0);

            // spend the input
            utxoBuilder.Spend(input1, chainedBlock);

            // verify utxo storage
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage.ContainsKey(txHash));
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage[txHash].OutputStates.Length == 3);
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage[txHash].OutputStates[0] == OutputState.Spent);
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage[txHash].OutputStates[1] == OutputState.Unspent);
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage[txHash].OutputStates[2] == OutputState.Unspent);

            // create an input to spend the unspent transaction's second output
            var input2 = new TxInput(new TxOutputKey(txHash, txOutputIndex: 1), ImmutableArray.Create<byte>(), 0);

            // spend the input
            utxoBuilder.Spend(input2, chainedBlock);

            // verify utxo storage
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage.ContainsKey(txHash));
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage[txHash].OutputStates.Length == 3);
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage[txHash].OutputStates[0] == OutputState.Spent);
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage[txHash].OutputStates[1] == OutputState.Spent);
            Assert.IsTrue(memoryUtxoBuilderStorage.Storage[txHash].OutputStates[2] == OutputState.Unspent);

            // create an input to spend the unspent transaction's third output
            var input3 = new TxInput(new TxOutputKey(txHash, txOutputIndex: 2), ImmutableArray.Create<byte>(), 0);

            // spend the input
            utxoBuilder.Spend(input3, chainedBlock);

            // verify utxo storage
            Assert.IsFalse(memoryUtxoBuilderStorage.Storage.ContainsKey(txHash));
        }

        [TestMethod]
        [ExpectedException(typeof(ValidationException))]
        public void TestDoubleSpend()
        {
            // prepare block
            var chainedBlock = new ChainedBlock(blockHash: 1, previousBlockHash: 0, height: 0, totalWork: 0);

            // prepare an unspent transaction
            var txHash = new UInt256(100);
            var unspentTx = new UnspentTx(txHash, 1, OutputState.Unspent);

            // mock a parent utxo containing the unspent transaction
            var mockParentUtxoStorage = new Mock<IUtxoStorage>();
            mockParentUtxoStorage.Setup(utxo => utxo.UnspentTransactions()).Returns(new[] { unspentTx });
            var parentUtxo = new Utxo(mockParentUtxoStorage.Object);

            // initialize memory utxo builder storage
            var memoryUtxoBuilderStorage = new MemoryUtxoBuilderStorage(mockParentUtxoStorage.Object);

            // mock a cache context
            var mockCacheContext = new Mock<ICacheContext>();
            mockCacheContext.Setup(cc => cc.ToUtxoBuilder(mockParentUtxoStorage.Object)).Returns(memoryUtxoBuilderStorage);

            // initialize utxo builder
            var utxoBuilder = new UtxoBuilder(mockCacheContext.Object, parentUtxo);

            // create an input to spend the unspent transaction
            var input = new TxInput(new TxOutputKey(txHash, txOutputIndex: 0), ImmutableArray.Create<byte>(), 0);

            // spend the input
            utxoBuilder.Spend(input, chainedBlock);

            // verify utxo storage
            Assert.IsFalse(memoryUtxoBuilderStorage.Storage.ContainsKey(txHash));

            // attempt to spend the input again
            utxoBuilder.Spend(input, chainedBlock);

            // validation exception should be thrown
        }
    }
}
