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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class TxLoader : BlockingCollectionWorker<PendingTx>
    {
        private readonly IBlockStorageNew blockCache;
        private readonly TxValidator txValidator;
        private readonly IBlockchainRules rules;
        private ConcurrentBag<Exception> exceptions;

        public TxLoader(IBlockStorageNew blockCache, TxValidator txValidator, Logger logger, IBlockchainRules rules, bool isConcurrent)
            : base("TxLoader", isConcurrent, logger: logger)
        {
            this.blockCache = blockCache;
            this.txValidator = txValidator;
            this.rules = rules;
        }

        public ConcurrentBag<Exception> Exceptions
        {
            get { return this.exceptions; }
        }

        protected override void SubStart()
        {
            this.exceptions = new ConcurrentBag<Exception>();
        }

        protected override void ConsumeItem(PendingTx pendingTx)
        {
            try
            {
                var txIndex = pendingTx.txIndex;
                var transaction = pendingTx.transaction;
                var chainedHeader = pendingTx.chainedHeader;
                var isCoinbase = pendingTx.isCoinbase;
                var spentTxes = pendingTx.spentTxes;

                var prevTxOutputs = ImmutableArray.CreateBuilder<TxOutput>(transaction.Inputs.Length);

                // load previous transactions for each input, unless this is a coinbase transaction
                if (!isCoinbase)
                {
                    for (var inputIndex = 0; inputIndex < transaction.Inputs.Length; inputIndex++)
                    {
                        var input = transaction.Inputs[inputIndex];
                        var spentTx = spentTxes[inputIndex];

                        Transaction prevTx;
                        if (this.blockCache.TryGetTransaction(spentTx.blockHash, spentTx.txIndex, out prevTx))
                        {
                            if (input.PreviousTxOutputKey.TxHash != prevTx.Hash)
                                throw new Exception("TODO");

                            var prevTxOutput = prevTx.Outputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()];
                            prevTxOutputs.Add(prevTxOutput);
                        }
                        else
                        {
                            throw new Exception("TODO");
                        }
                    }
                }

                if (!MainnetRules.IgnoreScripts)
                {
                    var pendingTxValid = new PendingTxValid(txIndex, transaction, chainedHeader, isCoinbase, prevTxOutputs.ToImmutable());
                    this.txValidator.Add(pendingTxValid);
                }
            }
            catch (Exception e)
            {
                this.exceptions.Add(e);
            }
        }

        protected override void CompletedItems()
        {
            this.txValidator.CompleteAdding();
        }
    }
}
