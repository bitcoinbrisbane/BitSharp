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
    public class TxValidator : BlockingCollectionWorker<TxWithPrevOutputs>
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

        protected override void ConsumeItem(TxWithPrevOutputs item)
        {
            try
            {
                //TODO validate transaction itself

                var chainedHeader = item.ChainedHeader;
                var transaction = item.Transaction;
                var txIndex = item.TxIndex;
                var prevTxOutputs = item.PrevTxOutputs;

                if (txIndex > 0)
                {
                    for (var inputIndex = 0; inputIndex < transaction.Inputs.Length; inputIndex++)
                    {
                        var txInput = transaction.Inputs[inputIndex];
                        var prevTxOutput = prevTxOutputs[inputIndex];

                        var txInputWithPrevOutput = new TxInputWithPrevOutput(chainedHeader, transaction, txIndex, txInput, inputIndex, prevTxOutput);
                        this.scriptValidator.Add(txInputWithPrevOutput);
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
