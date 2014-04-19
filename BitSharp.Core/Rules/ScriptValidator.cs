using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Rules
{
    public class ScriptValidator : ProducerConsumerWorker<Tuple<ChainPosition, ChainedHeader, Transaction, TxInput, TxOutput>>, IChainStateVisitor
    {
        private readonly IBlockchainRules rules;
        private readonly ConcurrentBag<Exception> validationExceptions;

        public ScriptValidator(Logger logger, IBlockchainRules rules)
            : base("ScriptValidator", isConcurrent: true, logger: logger)
        {
            this.rules = rules;
            this.validationExceptions = new ConcurrentBag<Exception>();

        }

        public ConcurrentBag<Exception> ValidationExceptions
        {
            get { return this.validationExceptions; }
        }

        protected override void ConsumeItem(Tuple<ChainPosition, ChainedHeader, Transaction, TxInput, TxOutput> value)
        {
            try
            {
                var chainPosition = value.Item1;
                var chainedHeader = value.Item2;
                var tx = value.Item3;
                var txInput = value.Item4;
                var txOutput = value.Item5;

                this.rules.ValidationTransactionScript(chainedHeader, tx, chainPosition.TxIndex, txInput, chainPosition.InputIndex, txOutput);
            }
            catch (Exception e)
            {
                this.validationExceptions.Add(e);
            }
        }

        void IChainStateVisitor.SpendTxOutput(ChainPosition chainPosition, ChainedHeader chainedHeader, Transaction tx, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash)
        {
            this.Add(Tuple.Create(chainPosition, chainedHeader, tx, txInput, txOutput));
        }

        void IChainStateVisitor.BeginBlock(ChainedHeader chainedHeader) { }
        void IChainStateVisitor.BeforeAddTransaction(ChainPosition chainPosition, Transaction tx) { }
        void IChainStateVisitor.CoinbaseInput(ChainPosition chainPosition, TxInput txInput) { }
        void IChainStateVisitor.MintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase) { }
        void IChainStateVisitor.AfterAddTransaction(ChainPosition chainPosition, Transaction tx) { }
        void IChainStateVisitor.BeforeRemoveTransaction(ChainPosition chainPosition, Transaction tx) { }
        void IChainStateVisitor.UnCoinbaseInput(ChainPosition chainPosition, TxInput txInput) { }
        void IChainStateVisitor.UnmintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase) { }
        void IChainStateVisitor.UnspendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash) { }
        void IChainStateVisitor.AfterRemoveTransaction(ChainPosition chainPosition, Transaction tx) { }
        void IChainStateVisitor.CommitBlock(ChainedHeader chainedHeader) { }
        void IChainStateVisitor.RollbackBlock(ChainedHeader chainedHeader) { }
    }
}
