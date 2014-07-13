using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using System.Collections;
using NLog;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Workers;
using System.Security.Cryptography;
using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using BitSharp.Domain;
using Ninject;
using Ninject.Parameters;

namespace BitSharp.Core.Builders
{
    internal class UtxoBuilder
    {
        private static readonly int DUPE_COINBASE_1_HEIGHT = 91722;
        private static readonly UInt256 DUPE_COINBASE_1_HASH = UInt256.Parse("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468", NumberStyles.HexNumber);
        private static readonly int DUPE_COINBASE_2_HEIGHT = 91812;
        private static readonly UInt256 DUPE_COINBASE_2_HASH = UInt256.Parse("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599", NumberStyles.HexNumber);

        private readonly Logger logger;
        private readonly SHA256Managed sha256;

        private readonly IChainStateBuilderStorage chainStateBuilderStorage;

        public UtxoBuilder(IChainStateBuilderStorage chainStateBuilderStorage, Logger logger)
        {
            this.logger = logger;
            this.sha256 = new SHA256Managed();

            this.chainStateBuilderStorage = chainStateBuilderStorage;
        }

        public IEnumerable<TxWithPrevOutputKeys> CalculateUtxo(Chain chain, IEnumerable<Transaction> blockTxes)
        {
            var chainedHeader = chain.LastBlock;

            this.chainStateBuilderStorage.PrepareSpentTransactions(chainedHeader.Height);

            var txIndex = -1;
            foreach (var tx in blockTxes)
            {
                txIndex++;

                // there exist two duplicate coinbases in the blockchain, which the design assumes to be impossible
                // ignore the first occurrences of these duplicates so that they do not need to later be deleted from the utxo, an unsupported operation
                // no other duplicates will occur again, it is now disallowed
                if ((chainedHeader.Height == DUPE_COINBASE_1_HEIGHT && tx.Hash == DUPE_COINBASE_1_HASH)
                    || (chainedHeader.Height == DUPE_COINBASE_2_HEIGHT && tx.Hash == DUPE_COINBASE_2_HASH))
                {
                    continue;
                }

                var prevOutputTxKeys = ImmutableArray.CreateBuilder<BlockTxKey>(tx.Inputs.Length);

                //TODO apply real coinbase rule
                // https://github.com/bitcoin/bitcoin/blob/481d89979457d69da07edd99fba451fd42a47f5c/src/core.h#L219
                if (txIndex > 0)
                {
                    // spend each of the transaction's inputs in the utxo
                    for (var inputIndex = 0; inputIndex < tx.Inputs.Length; inputIndex++)
                    {
                        var input = tx.Inputs[inputIndex];
                        var unspentTx = this.Spend(txIndex, tx, inputIndex, input, chainedHeader);

                        var unspentTxBlockHash = chain.Blocks[unspentTx.BlockIndex].Hash;
                        prevOutputTxKeys.Add(new BlockTxKey(unspentTxBlockHash, unspentTx.TxIndex));
                    }
                }

                // mint the transaction's outputs in the utxo
                this.Mint(tx, txIndex, chainedHeader);

                yield return new TxWithPrevOutputKeys(txIndex, tx, chainedHeader, prevOutputTxKeys.ToImmutable());
            }
        }

        private void Mint(Transaction tx, int txIndex, ChainedHeader chainedHeader)
        {
            // add transaction to the utxo
            var unspentTx = new UnspentTx(chainedHeader.Height, txIndex, tx.Outputs.Length, OutputState.Unspent);
            if (!this.chainStateBuilderStorage.TryAddTransaction(tx.Hash, unspentTx))
            {
                // duplicate transaction
                this.logger.Warn("Duplicate transaction at block {0:#,##0}, {1}, coinbase".Format2(chainedHeader.Height, chainedHeader.Hash.ToHexNumberString()));
                throw new ValidationException(chainedHeader.Hash);
            }
        }

        private UnspentTx Spend(int txIndex, Transaction tx, int inputIndex, TxInput input, ChainedHeader chainedHeader)
        {
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            {
                // output wasn't present in utxo, invalid block
                throw new ValidationException(chainedHeader.Hash);
            }

            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);

            if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
            {
                // output was out of bounds
                throw new ValidationException(chainedHeader.Hash);
            }

            if (unspentTx.OutputStates[outputIndex] == OutputState.Spent)
            {
                // output was already spent
                throw new ValidationException(chainedHeader.Hash);
            }

            // update output states
            unspentTx = unspentTx.SetOutputState(outputIndex, OutputState.Spent);

            // update transaction output states in the utxo
            this.chainStateBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx);

            // remove fully spent transaction from the utxo
            if (unspentTx.OutputStates.All(x => x == OutputState.Spent))
            {
                this.chainStateBuilderStorage.RemoveTransaction(input.PreviousTxOutputKey.TxHash, chainedHeader.Height);
            }

            return unspentTx;
        }

        //TODO with the rollback information that's now being stored, rollback could be down without needing the block
        public void RollbackUtxo(ChainedHeader chainedHeader, IEnumerable<BlockTx> blockTxes, ImmutableDictionary<UInt256, SpentTx> spentTxes)
        {
            //TODO don't reverse here, storage should be read in reverse
            foreach (var blockTx in blockTxes.Reverse())
            {
                var txIndex = blockTx.Index;

                if (txIndex == 0)
                {
                    var coinbaseTx = blockTx.Transaction;

                    // remove coinbase outputs
                    this.Unmint(coinbaseTx, chainedHeader, isCoinbase: true);
                }
                else
                {
                    var tx = blockTx.Transaction;

                    // remove outputs
                    this.Unmint(tx, chainedHeader, isCoinbase: false);

                    // remove inputs in reverse order
                    for (var inputIndex = tx.Inputs.Length - 1; inputIndex >= 0; inputIndex--)
                    {
                        var input = tx.Inputs[inputIndex];
                        this.Unspend(input, chainedHeader, spentTxes);
                    }
                }
            }
        }

        private void Unmint(Transaction tx, ChainedHeader chainedHeader, bool isCoinbase)
        {
            // ignore duplicate coinbases
            if ((chainedHeader.Height == DUPE_COINBASE_1_HEIGHT && tx.Hash == DUPE_COINBASE_1_HASH)
                || (chainedHeader.Height == DUPE_COINBASE_2_HEIGHT && tx.Hash == DUPE_COINBASE_2_HASH))
            {
                return;
            }

            // check that transaction exists
            UnspentTx unspentTx;
            if (!this.chainStateBuilderStorage.TryGetTransaction(tx.Hash, out unspentTx))
            {
                // missing transaction output
                this.logger.Warn("Missing transaction at block {0:#,##0}, {1}, tx {2}".Format2(chainedHeader.Height, chainedHeader.Hash.ToHexNumberString(), tx.Hash));
                throw new ValidationException(chainedHeader.Hash);
            }

            //TODO verify blockheight

            // verify all outputs are unspent before unminting
            if (!unspentTx.OutputStates.All(x => x == OutputState.Unspent))
            {
                throw new ValidationException(chainedHeader.Hash);
            }

            // remove the transaction
            this.chainStateBuilderStorage.RemoveTransaction(tx.Hash, spentBlockIndex: -1);
        }

        private void Unspend(TxInput input, ChainedHeader chainedHeader, ImmutableDictionary<UInt256, SpentTx> spentTxes)
        {
            bool wasRestored;

            UnspentTx unspentTx;
            if (this.chainStateBuilderStorage.TryGetTransaction(input.PreviousTxOutputKey.TxHash, out unspentTx))
            {
                wasRestored = false;
            }
            else
            {
                // lookup fully spent transaction
                SpentTx spentTx;
                if (!spentTxes.TryGetValue(input.PreviousTxOutputKey.TxHash, out spentTx))
                    throw new ValidationException(chainedHeader.Hash);

                // restore fully spent transaction
                unspentTx = new UnspentTx(spentTx.ConfirmedBlockIndex, spentTx.TxIndex, new OutputStates(spentTx.OutputCount, OutputState.Spent));
                wasRestored = true;
            }

            // retrieve previous output index
            var outputIndex = unchecked((int)input.PreviousTxOutputKey.TxOutputIndex);
            if (outputIndex < 0 || outputIndex >= unspentTx.OutputStates.Length)
                throw new Exception("TODO - corruption");

            // check that output isn't already considered unspent
            if (unspentTx.OutputStates[outputIndex] == OutputState.Unspent)
                throw new ValidationException(chainedHeader.Hash);

            // mark output as unspent
            unspentTx = unspentTx.SetOutputState(outputIndex, OutputState.Unspent);

            // update storage
            if (!wasRestored)
            {
                this.chainStateBuilderStorage.UpdateTransaction(input.PreviousTxOutputKey.TxHash, unspentTx);
            }
            else
            {
                // a restored fully spent transaction must be added back
                var wasAdded = this.chainStateBuilderStorage.TryAddTransaction(input.PreviousTxOutputKey.TxHash, unspentTx);
                if (!wasAdded)
                    throw new ValidationException(chainedHeader.Hash);
            }
        }
    }
}