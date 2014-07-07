using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Test;
using BitSharp.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent.Test
{
    [TestClass]
    public class RollbackTest
    {
        [TestCleanup]
        public void Cleanup()
        {
            EsentTests.CleanBaseDirectory();
        }

        [TestMethod]
        public void TestRollback()
        {
            var logger = LogManager.CreateNullLogger();
            var baseDirectory = EsentTests.PrepareBaseDirectory();
            var sha256 = new SHA256Managed();

            var blockProvider = new MainnetBlockProvider();
            var blocks = Enumerable.Range(0, 1000).Select(x => blockProvider.GetBlock(x)).ToList();

            var genesisBlock = blocks[0];
            var genesisHeader = new ChainedHeader(genesisBlock.Header, height: 0, totalWork: 0);
            var genesisChain = Chain.CreateForGenesisBlock(genesisHeader);
            var genesisUtxo = Utxo.CreateForGenesisBlock(genesisBlock.Hash);
            var chainBuilder = genesisChain.ToBuilder();

            var rules = new MainnetRules(logger, null);

            using (var blockStorage = new BlockStorageNew(baseDirectory))
            using (var chainStateBuilderStorage = new ChainStateBuilderStorage(baseDirectory, genesisUtxo.Storage, logger))
            using (var chainStateBuilder = new ChainStateBuilder(chainBuilder, chainStateBuilderStorage, logger, rules, blockStorage, null, null))
            {
                // add blocks to storage
                foreach (var block in blocks)
                    blockStorage.AddBlock(block);

                // store the genesis utxo state
                var expectedUtxos = new List<List<KeyValuePair<UInt256, UnspentTx>>>();
                using (var chainState = chainStateBuilder.ToImmutable())
                {
                    expectedUtxos.Add(chainState.Utxo.GetUnspentTransactions().ToList());
                }

                // calculate utxo forward and store its state at each step along the way
                for (var blockIndex = 1; blockIndex < blocks.Count; blockIndex++)
                {
                    var block = blocks[blockIndex];
                    var chainedHeader = new ChainedHeader(block.Header, blockIndex, 0);
                    var blockTxes = block.Transactions.Select((x, i) => new BlockTx(i, 0, x.Hash, x));

                    chainStateBuilder.AddBlock(chainedHeader, blockTxes);

                    using (var chainState = chainStateBuilder.ToImmutable())
                    {
                        expectedUtxos.Add(chainState.Utxo.GetUnspentTransactions().ToList());
                    }
                }

                // verify the utxo state before rolling back
                var expectedUtxoHash = UInt256.Parse("7e155a373c9a97d6d6f7f985e4d43f31a80833e9b4fce865c85552a650dd630e", NumberStyles.HexNumber);
                using (var utxoStream = new UtxoStream(logger, expectedUtxos.Last()))
                {
                    var utxoHash = new UInt256(sha256.ComputeDoubleHash(utxoStream));
                    Assert.AreEqual(expectedUtxoHash, utxoHash);
                }
                expectedUtxos.RemoveAt(expectedUtxos.Count - 1);

                // roll utxo backwards and validate its state at each step along the way
                for (var blockIndex = blocks.Count - 1; blockIndex >= 1; blockIndex--)
                {
                    var block = blocks[blockIndex];
                    var chainedHeader = new ChainedHeader(block.Header, blockIndex, 0);
                    var blockTxes = block.Transactions.Select((x, i) => new BlockTx(i, 0, x.Hash, x));

                    chainStateBuilder.RollbackBlock(chainedHeader, blockTxes);

                    var expectedUtxo = expectedUtxos.Last();
                    expectedUtxos.RemoveAt(expectedUtxos.Count - 1);

                    List<KeyValuePair<UInt256, UnspentTx>> actualUtxo;
                    using (var chainState = chainStateBuilder.ToImmutable())
                    {
                        actualUtxo = chainState.Utxo.GetUnspentTransactions().ToList();
                    }

                    CollectionAssert.AreEqual(expectedUtxo, actualUtxo, new UtxoComparer(), "UTXO differs at height: {0}".Format2(blockIndex));
                }
            }
        }

        private class UtxoComparer : IComparer, IComparer<KeyValuePair<UInt256, UnspentTx>>
        {
            public int Compare(KeyValuePair<UInt256, UnspentTx> x, KeyValuePair<UInt256, UnspentTx> y)
            {
                if (x.Key == y.Key && x.Value.Equals(y.Value))
                    return 0;
                else
                    return -1;
            }

            int IComparer.Compare(object x, object y)
            {
                return this.Compare((KeyValuePair<UInt256, UnspentTx>)x, (KeyValuePair<UInt256, UnspentTx>)y);
            }
        }
    }
}
