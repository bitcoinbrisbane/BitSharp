using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    internal class TxPrevOutputLoader : BlockingCollectionWorker<TxWithPrevOutputKeys>
    {
        private readonly IBlockStorageNew blockCache;
        private readonly TxValidator txValidator;
        private readonly IBlockchainRules rules;
        private readonly Logger logger;

        private ConcurrentDictionary<UInt256, Transaction> txCache;
        private ConcurrentBag<Exception> exceptions;
        private int txCount;
        private int inputCount;

        public TxPrevOutputLoader(IBlockStorageNew blockCache, TxValidator txValidator, Logger logger, IBlockchainRules rules, bool isConcurrent)
            : base("TxPrevOutputLoader", isConcurrent, logger: logger)
        {
            this.blockCache = blockCache;
            this.txValidator = txValidator;
            this.rules = rules;
            this.logger = logger;
        }

        public ConcurrentBag<Exception> Exceptions
        {
            get { return this.exceptions; }
        }

        public int TxCount
        {
            get { return this.txCount; }
        }

        public int InputCount
        {
            get { return this.inputCount; }
        }

        protected override void SubStart()
        {
            this.txCache = new ConcurrentDictionary<UInt256, Transaction>();
            this.exceptions = new ConcurrentBag<Exception>();
            this.txCount = 0;
            this.inputCount = 0;
        }

        protected override void ConsumeItem(TxWithPrevOutputKeys item)
        {
            try
            {
                if (MainnetRules.BypassValidation)
                    return;

                this.txCount++;

                var txIndex = item.txIndex;
                var transaction = item.transaction;
                var chainedHeader = item.chainedHeader;
                var spentTxes = item.prevOutputTxKeys;

                var prevTxOutputs = ImmutableArray.CreateBuilder<TxOutput>(transaction.Inputs.Length);

                // load previous transactions for each input, unless this is a coinbase transaction
                if (txIndex > 0)
                {
                    this.inputCount += transaction.Inputs.Length;

                    for (var inputIndex = 0; inputIndex < transaction.Inputs.Length; inputIndex++)
                    {
                        var input = transaction.Inputs[inputIndex];

                        Transaction cachedPrevTx;
                        if (this.txCache.TryGetValue(input.PreviousTxOutputKey.TxHash, out cachedPrevTx))
                        {
                            var prevTxOutput = cachedPrevTx.Outputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()];
                            prevTxOutputs.Add(prevTxOutput);
                        }
                        else
                        {
                            var spentTx = spentTxes[inputIndex];

                            Transaction prevTx;
                            if (this.blockCache.TryGetTransaction(spentTx.BlockHash, spentTx.TxIndex, out prevTx))
                            {
                                if (input.PreviousTxOutputKey.TxHash != prevTx.Hash)
                                    throw new Exception("TODO");

                                this.txCache.TryAdd(prevTx.Hash, prevTx);

                                var prevTxOutput = prevTx.Outputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()];
                                prevTxOutputs.Add(prevTxOutput);
                            }
                            else
                            {
                                throw new Exception("TODO");
                            }
                        }
                    }

                    Debug.Assert(prevTxOutputs.Count == transaction.Inputs.Length);
                }

                if (!MainnetRules.IgnoreScripts)
                {
                    var txWithPrevOutputs = new TxWithPrevOutputs(txIndex, transaction, chainedHeader, prevTxOutputs.ToImmutableArray());
                    this.txValidator.Add(txWithPrevOutputs);
                }
            }
            catch (Exception e)
            {
                this.exceptions.Add(e);
            }
        }

        protected override void CompletedItems()
        {
            this.txCache = null;
            this.txValidator.CompleteAdding();
        }
    }
}
