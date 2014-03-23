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
            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // prepare an unspent transaction
            var txHash = new UInt256(100);
            var txOutputIndex = 0U;
            var txUnspentOutputs = new ImmutableBitArray(1, false);
            var unspentTx = new UnspentTx(txHash, 1, OutputState.Unspent);

            // mock a parent utxo containing the unspent transaction
            var mockParentUtxo = new Mock<Utxo>();
            mockParentUtxo.Setup(utxo => utxo.UnspentTransactions()).Returns(new[] { unspentTx });

            // initialize utxo builder
            var utxoBuilder = new UtxoBuilder(memoryCacheContext, mockParentUtxo.Object);

            // create an input to spend the unspent transaction
            var input = new TxInput(new TxOutputKey(txHash, txOutputIndex), ImmutableList.Create<byte>(), 0);

            // spend the input
            utxoBuilder.Spend(input);

            // no validation exception thrown
        }

        [TestMethod]
        [ExpectedException(typeof(ValidationException))]
        public void TestDoubleSpend()
        {
            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // prepare an unspent transaction
            var txHash = new UInt256(100);
            var txOutputIndex = 0U;
            var txUnspentOutputs = new ImmutableBitArray(1, false);
            var unspentTx = new UnspentTx(txHash, 1, OutputState.Unspent);

            // mock a parent utxo containing the unspent transaction
            var mockParentUtxo = new Mock<Utxo>();
            mockParentUtxo.Setup(utxo => utxo.UnspentTransactions()).Returns(new[] { unspentTx });

            // initialize utxo builder
            var utxoBuilder = new UtxoBuilder(memoryCacheContext, mockParentUtxo.Object);

            // create an input to spend the unspent transaction
            var input = new TxInput(new TxOutputKey(txHash, txOutputIndex), ImmutableList.Create<byte>(), 0);

            // spend the input
            utxoBuilder.Spend(input);

            // attempt to spend the input again
            utxoBuilder.Spend(input);

            // validation exception should be thrown
        }
    }
}
