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
        public void TestRollback()
        {
            RunTest(TestRollback);
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

            var rules = new MainnetRules(logger);

            using (var storageManager = provider.OpenStorageManager())
            using (var coreStorage = new CoreStorage(storageManager, logger))
            using (var chainStateBuilder = new ChainStateBuilder(logger, rules, /*TODO*/null, coreStorage))
            {
                // add blocks to storage
                coreStorage.AddGenesisBlock(ChainedHeader.CreateForGenesisBlock(blocks[0].Header));
                foreach (var block in blocks)
                    coreStorage.TryAddBlock(block);

                // calculate utxo forward and store its state at each step along the way
                var expectedUtxos = new List<List<UnspentTx>>();
                for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
                {
                    var block = blocks[blockIndex];
                    var chainedHeader = new ChainedHeader(block.Header, blockIndex, 0);

                    chainStateBuilder.AddBlock(chainedHeader, block.Transactions);

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

                    List<UnspentTx> actualUtxo;
                    using (var chainState = chainStateBuilder.ToImmutable())
                    {
                        actualUtxo = chainState.Utxo.GetUnspentTransactions().ToList();
                    }

                    CollectionAssert.AreEqual(expectedUtxo, actualUtxo, "UTXO differs at height: {0}".Format2(blockIndex));
                }
            }
        }
    }
}
