using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using BitSharp.Core.Rules;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class TxValidator : BlockingCollectionWorker<PendingTxValid>
    {
        private readonly ScriptValidator scriptValidator;
        private readonly IBlockchainRules rules;
        private ConcurrentBag<Exception> exceptions;

        public TxValidator(ScriptValidator scriptValidator, Logger logger, IBlockchainRules rules, bool isConcurrent)
            : base("TxValidator", isConcurrent, logger: logger)
        {
            this.scriptValidator = scriptValidator;
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

        protected override void ConsumeItem(PendingTxValid pendingTx)
        {
            try
            {
                //TODO validate transaction itself

                var chainedHeader = pendingTx.chainedHeader;
                var transaction = pendingTx.transaction;
                var txIndex = pendingTx.txIndex;
                var isCoinbase = pendingTx.isCoinbase;
                var prevTxOutputs = pendingTx.prevTxOutputs;

                if (!isCoinbase)
                {
                    for (var inputIndex = 0; inputIndex < transaction.Inputs.Length; inputIndex++)
                    {
                        var txInput = transaction.Inputs[inputIndex];
                        var prevTxOutput = prevTxOutputs[inputIndex];

                        var pendingScript = new PendingScript(chainedHeader, transaction, txIndex, txInput, inputIndex, prevTxOutput);
                        this.scriptValidator.Add(pendingScript);
                    }
                }
            }
            catch (Exception e)
            {
                this.exceptions.Add(e);
            }
        }

        protected override void CompletedItems()
        {
            this.scriptValidator.CompleteAdding();
        }
    }
}
