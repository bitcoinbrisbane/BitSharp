using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Builders;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Ninject;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace BitSharp.Core.Test.Builders
{
    [TestClass]
    public class ChainStateBuilderTest
    {
        [TestMethod]
        public void TestSimpleSpend()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new MemoryStorageModule());
            var shutdownToken = new CancellationTokenSource();

            // prepare block
            var fakeHeaders = new FakeHeaders();
            var chainedHeader = new ChainedHeader(fakeHeaders.Genesis(), height: 0, totalWork: 0);

            // prepare an unspent transaction
            var txHash = new UInt256(100);
            var unspentTx = new UnspentTx(chainedHeader.Hash, 3, OutputState.Unspent);

            // prepare unspent output
            var unspentTransactions = ImmutableDictionary.Create<UInt256, UnspentTx>().Add(txHash, unspentTx);
            var unspentOutputs = ImmutableDictionary.Create<TxOutputKey, TxOutput>()
                .Add(new TxOutputKey(txHash, 0), new TxOutput(0, ImmutableArray.Create<byte>()))
                .Add(new TxOutputKey(txHash, 1), new TxOutput(0, ImmutableArray.Create<byte>()))
                .Add(new TxOutputKey(txHash, 2), new TxOutput(0, ImmutableArray.Create<byte>()));

            // mock a parent utxo containing the unspent transaction
            var mockParentChainStateStorage = new Mock<IChainStateStorage>();
            mockParentChainStateStorage.Setup(utxo => utxo.UnspentTransactions()).Returns(unspentTransactions);
            mockParentChainStateStorage.Setup(utxo => utxo.UnspentOutputs()).Returns(unspentOutputs);
            var parentUtxo = new Utxo(mockParentChainStateStorage.Object);

            // initialize memory utxo builder storage
            var memoryChainStateBuilderStorage = new MemoryChainStateBuilderStorage(mockParentChainStateStorage.Object);
            kernel.Rebind<IChainStateBuilderStorage>().ToConstant(memoryChainStateBuilderStorage);

            // initialize utxo builder
            var chainStateBuilder = new ChainStateBuilder(null, null, null, parentUtxo, shutdownToken.Token, LogManager.CreateNullLogger(), kernel, null, null, null, null, null);

            // create an input to spend the unspent transaction's first output
            var input1 = new TxInput(new TxOutputKey(txHash, txOutputIndex: 0), ImmutableArray.Create<byte>(), 0);

            // spend the input
            chainStateBuilder.Spend(input1, chainedHeader);

            // verify utxo storage
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary.ContainsKey(txHash));
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary[txHash].OutputStates.Length == 3);
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary[txHash].OutputStates[0] == OutputState.Spent);
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary[txHash].OutputStates[1] == OutputState.Unspent);
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary[txHash].OutputStates[2] == OutputState.Unspent);

            // create an input to spend the unspent transaction's second output
            var input2 = new TxInput(new TxOutputKey(txHash, txOutputIndex: 1), ImmutableArray.Create<byte>(), 0);

            // spend the input
            chainStateBuilder.Spend(input2, chainedHeader);

            // verify utxo storage
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary.ContainsKey(txHash));
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary[txHash].OutputStates.Length == 3);
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary[txHash].OutputStates[0] == OutputState.Spent);
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary[txHash].OutputStates[1] == OutputState.Spent);
            Assert.IsTrue(memoryChainStateBuilderStorage.UnspentTransactionsDictionary[txHash].OutputStates[2] == OutputState.Unspent);

            // create an input to spend the unspent transaction's third output
            var input3 = new TxInput(new TxOutputKey(txHash, txOutputIndex: 2), ImmutableArray.Create<byte>(), 0);

            // spend the input
            chainStateBuilder.Spend(input3, chainedHeader);

            // verify utxo storage
            Assert.IsFalse(memoryChainStateBuilderStorage.UnspentTransactionsDictionary.ContainsKey(txHash));
        }

        [TestMethod]
        [ExpectedException(typeof(ValidationException))]
        public void TestDoubleSpend()
        {
            // prepare test kernel
            var kernel = new StandardKernel(new MemoryStorageModule());
            var shutdownToken = new CancellationTokenSource();

            // prepare block
            var fakeHeaders = new FakeHeaders();
            var chainedHeader = new ChainedHeader(fakeHeaders.Genesis(), height: 0, totalWork: 0);

            // prepare an unspent transaction
            var txHash = new UInt256(100);
            var unspentTx = new UnspentTx(chainedHeader.Hash, 1, OutputState.Unspent);

            // mock a parent utxo containing the unspent transaction
            var unspentTransactions = ImmutableDictionary.Create<UInt256, UnspentTx>().Add(txHash, unspentTx);
            var mockParentChainStateStorage = new Mock<IChainStateStorage>();
            mockParentChainStateStorage.Setup(utxo => utxo.UnspentTransactions()).Returns(unspentTransactions);
            var parentUtxo = new Utxo(mockParentChainStateStorage.Object);

            // initialize memory utxo builder storage
            var memoryChainStateBuilderStorage = new MemoryChainStateBuilderStorage(mockParentChainStateStorage.Object);
            kernel.Rebind<IChainStateBuilderStorage>().ToConstant(memoryChainStateBuilderStorage);

            // initialize utxo builder
            var chainStateBuilder = new ChainStateBuilder(null, null, null, parentUtxo, shutdownToken.Token, LogManager.CreateNullLogger(), kernel, null, null, null, null, null);

            // create an input to spend the unspent transaction
            var input = new TxInput(new TxOutputKey(txHash, txOutputIndex: 0), ImmutableArray.Create<byte>(), 0);

            // spend the input
            chainStateBuilder.Spend(input, chainedHeader);

            // verify utxo storage
            Assert.IsFalse(memoryChainStateBuilderStorage.UnspentTransactionsDictionary.ContainsKey(txHash));

            // attempt to spend the input again
            chainStateBuilder.Spend(input, chainedHeader);

            // validation exception should be thrown
        }
    }
}
