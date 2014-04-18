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
    public class ChainStateMonitor : Worker, IChainStateVisitor
    {
        private readonly ConcurrentSetBuilder<IChainStateVisitor> visitors;
        private readonly ConcurrentQueue<Action> actionQueue;

        public ChainStateMonitor(Logger logger)
            : base("ChainStateMonitor", initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger)
        {
            this.visitors = new ConcurrentSetBuilder<IChainStateVisitor>();
            this.actionQueue = new ConcurrentQueue<Action>();
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
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeginBlock(chainedHeader);
            });
            this.NotifyWork();
        }

        public void BeforeAddTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeforeAddTransaction(chainPosition, tx);
            });
            this.NotifyWork();
        }

        public void CoinbaseInput(ChainPosition chainPosition, TxInput txInput)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.CoinbaseInput(chainPosition, txInput);
            });
            this.NotifyWork();
        }

        public void MintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.MintTxOutput(chainPosition, txOutputKey, txOutput, outputScriptHash, isCoinbase);
            });
            this.NotifyWork();
        }

        public void SpendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.SpendTxOutput(chainPosition, txInput, txOutputKey, txOutput, outputScriptHash);
            });
            this.NotifyWork();
        }

        public void AfterAddTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.AfterAddTransaction(chainPosition, tx);
            });
            this.NotifyWork();
        }

        public void BeforeRemoveTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeforeRemoveTransaction(chainPosition, tx);
            });
            this.NotifyWork();
        }

        public void UnCoinbaseInput(ChainPosition chainPosition, TxInput txInput)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnCoinbaseInput(chainPosition, txInput);
            });
            this.NotifyWork();
        }

        public void UnmintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnmintTxOutput(chainPosition, txOutputKey, txOutput, outputScriptHash, isCoinbase);
            });
            this.NotifyWork();
        }

        public void UnspendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnspendTxOutput(chainPosition, txInput, txOutputKey, txOutput, outputScriptHash);
            });
            this.NotifyWork();
        }

        public void AfterRemoveTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.AfterRemoveTransaction(chainPosition, tx);
            });
            this.NotifyWork();
        }

        public void CommitBlock(ChainedHeader chainedHeader)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.CommitBlock(chainedHeader);
            });
            this.NotifyWork();
        }

        public void RollbackBlock(ChainedHeader chainedHeader)
        {
            this.actionQueue.Enqueue(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.RollbackBlock(chainedHeader);
            });
            this.NotifyWork();
        }

        protected override void WorkAction()
        {
            Action action;
            while (this.IsStarted && this.actionQueue.TryDequeue(out action))
                action();
        }

        private sealed class Unsubscriber : IDisposable
        {
            private readonly ConcurrentSetBuilder<IChainStateVisitor> visitors;
            private readonly IChainStateVisitor visitor;

            public Unsubscriber(ConcurrentSetBuilder<IChainStateVisitor> visitors, IChainStateVisitor visitor)
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
