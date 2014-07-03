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
    public class ScriptValidator : BlockingCollectionWorker<PendingScript>
    {
        private readonly IBlockchainRules rules;
        private ConcurrentBag<Exception> exceptions;

        public ScriptValidator(Logger logger, IBlockchainRules rules, bool isConcurrent)
            : base("ScriptValidator", isConcurrent, logger: logger)
        {
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

        protected override void ConsumeItem(PendingScript pendingScript)
        {
            try
            {
                this.rules.ValidationTransactionScript(pendingScript.chainedHeader, pendingScript.transaction, pendingScript.txIndex, pendingScript.txInput, pendingScript.inputIndex, pendingScript.prevTxOutput);
            }
            catch (Exception e)
            {
                this.exceptions.Add(e);
            }
        }

        protected override void CompletedItems()
        {
        }
    }
}
