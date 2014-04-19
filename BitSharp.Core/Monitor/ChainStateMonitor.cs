using BitSharp.Common;
using BitSharp.Core.Domain;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Monitor
{
    public class ChainStateMonitor : ProducerConsumerWorker<Action>, IChainStateVisitor
    {
        private readonly ConcurrentSet<IChainStateVisitor> visitors;

        public ChainStateMonitor(Logger logger)
            : base("ChainStateMonitor", logger: logger)
        {
            this.visitors = new ConcurrentSet<IChainStateVisitor>();
        }

        protected override void SubDispose()
        {
            this.visitors.Clear();
        }

        public IDisposable Subscribe(IChainStateVisitor visitor)
        {
            this.visitors.Add(visitor);
            return new Unsubscriber(this.visitors, visitor);
        }

        public void BeginBlock(ChainedHeader chainedHeader)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeginBlock(chainedHeader);
            });
        }

        public void BeforeAddTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeforeAddTransaction(chainPosition, tx);
            });
        }

        public void CoinbaseInput(ChainPosition chainPosition, TxInput txInput)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.CoinbaseInput(chainPosition, txInput);
            });
        }

        public void MintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.MintTxOutput(chainPosition, txOutputKey, txOutput, outputScriptHash, isCoinbase);
            });
        }

        public void SpendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.SpendTxOutput(chainPosition, txInput, txOutputKey, txOutput, outputScriptHash);
            });
        }

        public void AfterAddTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.AfterAddTransaction(chainPosition, tx);
            });
        }

        public void BeforeRemoveTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeforeRemoveTransaction(chainPosition, tx);
            });
        }

        public void UnCoinbaseInput(ChainPosition chainPosition, TxInput txInput)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnCoinbaseInput(chainPosition, txInput);
            });
        }

        public void UnmintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnmintTxOutput(chainPosition, txOutputKey, txOutput, outputScriptHash, isCoinbase);
            });
        }

        public void UnspendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnspendTxOutput(chainPosition, txInput, txOutputKey, txOutput, outputScriptHash);
            });
        }

        public void AfterRemoveTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.AfterRemoveTransaction(chainPosition, tx);
            });
        }

        public void CommitBlock(ChainedHeader chainedHeader)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.CommitBlock(chainedHeader);
            });
        }

        public void RollbackBlock(ChainedHeader chainedHeader)
        {
            this.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.RollbackBlock(chainedHeader);
            });
        }

        protected override void ConsumeItem(Action action)
        {
            action();
        }

        private sealed class Unsubscriber : IDisposable
        {
            private readonly ConcurrentSet<IChainStateVisitor> visitors;
            private readonly IChainStateVisitor visitor;

            public Unsubscriber(ConcurrentSet<IChainStateVisitor> visitors, IChainStateVisitor visitor)
            {
                this.visitors = visitors;
                this.visitor = visitor;
            }

            public void Dispose()
            {
                this.visitors.Remove(this.visitor);
            }
        }
    }
}
