using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Test;
using BitSharp.Core.Test.Rules;
using BitSharp.Domain;
using BitSharp.Esent.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test.Storage
{
    [TestClass]
    public class StorageIntegrationTest : StorageProviderTest
    {
        [TestMethod]
        public void TestPrune()
        {
            RunTest(TestPrune);
        }

        [TestMethod]
        public void TestRollback()
        {
            RunTest(TestRollback);
        }

        private void TestPrune(ITestStorageProvider provider)
        {
            var logger = LogManager.CreateNullLogger();

            var txCount = 100;
            var transactions = Enumerable.Range(0, txCount).Select(x => RandomData.RandomTransaction()).ToImmutableArray();
            var blockHeader = RandomData.RandomBlockHeader().With(MerkleRoot: DataCalculator.CalculateMerkleRoot(transactions), Bits: DataCalculator.TargetToBits(UnitTestRules.Target0));
            var block = new Block(blockHeader, transactions);

            var expectedFinalDepth = (int)Math.Ceiling(Math.Log(txCount, 2));
            var expectedFinalElement = new BlockTx(index: 0, depth: expectedFinalDepth, hash: blockHeader.MerkleRoot, pruned: true, transaction: null);

            var pruneOrderSource = Enumerable.Range(0, txCount).ToList();
            var pruneOrder = new List<int>(txCount);

            var random = new Random();
            while (pruneOrderSource.Count > 0)
            {
                var randomIndex = random.Next(pruneOrderSource.Count);

                pruneOrder.Add(pruneOrderSource[randomIndex]);
                pruneOrderSource.RemoveAt(randomIndex);
            }

            using (var storageManager = provider.OpenStorageManager(logger))
            using (var coreStorage = new CoreStorage(storageManager, logger))
            {
                coreStorage.AddGenesisBlock(ChainedHeader.CreateForGenesisBlock(block.Header));
                coreStorage.TryAddBlock(block);
                var blockTxes = coreStorage.ReadBlockTransactions(block.Hash, block.Header.MerkleRoot).ToList();

                new MethodTimer().Time(() =>
                {
                    foreach (var pruneIndex in pruneOrder)
                    {
                        coreStorage.PruneElements(block.Hash, new[] { pruneIndex });
                        coreStorage.ReadBlockTransactions(block.Hash, block.Header.MerkleRoot).ToList();
                    }

                    var finalNodes = coreStorage.ReadBlockTransactions(block.Hash, block.Header.MerkleRoot).ToList();

                    Assert.AreEqual(1, finalNodes.Count);
                    Assert.AreEqual(expectedFinalElement, finalNodes[0]);
                });
            }
        }

        private void TestRollback(ITestStorageProvider provider)
        {
            var logger = LogManager.CreateNullLogger();
            var sha256 = new SHA256Managed();

            var blockProvider = new MainnetBlockProvider();
            var blocks = Enumerable.Range(0, 500).Select(x => blockProvider.GetBlock(x)).ToList();

            var genesisBlock = blocks[0];
            var genesisHeader = new ChainedHeader(genesisBlock.Header, height: 0, totalWork: 0);
            var genesisChain = Chain.CreateForGenesisBlock(genesisHeader);
            var genesisUtxo = Utxo.CreateForGenesisBlock(genesisHeader);

            var rules = new MainnetRules(logger);

            using (var storageManager = provider.OpenStorageManager(logger))
            using (var coreStorage = new CoreStorage(storageManager, logger))
            using (var chainStateBuilderStorage = storageManager.CreateOrLoadChainState(genesisHeader))
            using (var chainStateBuilder = new ChainStateBuilder(chainStateBuilderStorage, logger, rules, coreStorage))
            {
                // add blocks to storage
                coreStorage.AddGenesisBlock(ChainedHeader.CreateForGenesisBlock(blocks[0].Header));
                foreach (var block in blocks)
                    coreStorage.TryAddBlock(block);

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
                    var blockTxes = block.Transactions.Select((tx, txIndex) => new BlockTx(txIndex, 0, tx.Hash, /*pruned:*/false, tx));

                    chainStateBuilder.AddBlock(chainedHeader, blockTxes);

                    using (var chainState = chainStateBuilder.ToImmutable())
                    {
                        expectedUtxos.Add(chainState.Utxo.GetUnspentTransactions().ToList());
                    }
                }

                // verify the utxo state before rolling back
                //TODO verify the UTXO hash hard-coded here is correct
                var expectedUtxoHash = UInt256.Parse("609eb5882e0b71a707fb876c844fbfe6b4579e04eb27c7c0cefbb7478bac737b", NumberStyles.HexNumber);
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
                    var blockTxes = block.Transactions.Select((tx, txIndex) => new BlockTx(txIndex, 0, tx.Hash, /*pruned:*/false, tx));

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
