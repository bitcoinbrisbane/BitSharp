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
    public class UtxoTest
    {
        [TestMethod]
        public void TestCanSpend_Unspent()
        {
            // prepare utxo storage
            var utxoDictionary = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            // prepare unspent transaction
            var txHash = new UInt256(0);
            utxoDictionary.Add(txHash, new UnspentTx(txHash, 1, OutputState.Unspent));

            // prepare utxo
            var utxoStorage = new MemoryUtxoStorage(0, utxoDictionary.ToImmutable());
            var utxo = new Utxo(utxoStorage);

            // check if transcation can be spent
            var prevTxOutput = new TxOutputKey(txHash, txOutputIndex: 0);
            var canSpend = utxo.CanSpend(prevTxOutput);

            // verify transaction can be spent
            Assert.IsTrue(canSpend);
        }

        [TestMethod]
        public void TestCanSpend_Spent()
        {
            // prepare utxo storage
            var utxoDictionary = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            // prepare spent transaction
            var txHash = new UInt256(0);
            utxoDictionary.Add(txHash, new UnspentTx(txHash, 1, OutputState.Spent));

            // prepare utxo
            var utxoStorage = new MemoryUtxoStorage(0, utxoDictionary.ToImmutable());
            var utxo = new Utxo(utxoStorage);

            // check if transcation can be spent
            var prevTxOutput = new TxOutputKey(txHash, txOutputIndex: 0);
            var canSpend = utxo.CanSpend(prevTxOutput);

            // verify transaction cannot be spent
            Assert.IsFalse(canSpend);
        }

        [TestMethod]
        public void TestCanSpend_Missing()
        {
            // prepare utxo storage
            var utxoDictionary = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();

            // prepare utxo
            var utxoStorage = new MemoryUtxoStorage(0, utxoDictionary.ToImmutable());
            var utxo = new Utxo(utxoStorage);

            // check if missing transcation can be spent
            var prevTxOutput = new TxOutputKey(txHash: 0, txOutputIndex: 0);
            var canSpend = utxo.CanSpend(prevTxOutput);

            // verify missing transaction cannot be spent
            Assert.IsFalse(canSpend);
        }
    }
}
