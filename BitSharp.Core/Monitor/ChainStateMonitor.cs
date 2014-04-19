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
        private readonly ConcurrentSet<IChainStateVisitor> visitors;
        private ProducerConsumer<Action> actionQueue;

        public ChainStateMonitor(Logger logger)
            : base("ChainStateMonitor", initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger)
        {
            this.visitors = new ConcurrentSet<IChainStateVisitor>();
        }

        protected override void SubDispose()
        {
            this.visitors.Clear();
        }

        protected override void SubStart()
        {
            if (this.actionQueue != null)
                throw new InvalidOperationException();

            this.actionQueue = new ProducerConsumer<Action>();
            this.NotifyWork();
        }

        protected override void SubStop()
        {
            if (this.actionQueue == null || !this.actionQueue.IsCompleted)
                throw new InvalidOperationException();

            this.actionQueue.Dispose();
            this.actionQueue = null;
        }

        public IDisposable Subscribe(IChainStateVisitor visitor)
        {
            this.visitors.Add(visitor);
            return new Unsubscriber(this.visitors, visitor);
        }

        public void CompleteAdding()
        {
            if (this.actionQueue == null)
                throw new InvalidOperationException();

            this.actionQueue.CompleteAdding();
        }

        public void WaitToComplete()
        {
            if (this.actionQueue == null)
                throw new InvalidOperationException();

            this.actionQueue.WaitToComplete();
        }

        public void BeginBlock(ChainedHeader chainedHeader)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeginBlock(chainedHeader);
            });
        }

        public void BeforeAddTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeforeAddTransaction(chainPosition, tx);
            });
        }

        public void CoinbaseInput(ChainPosition chainPosition, TxInput txInput)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.CoinbaseInput(chainPosition, txInput);
            });
        }

        public void MintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.MintTxOutput(chainPosition, txOutputKey, txOutput, outputScriptHash, isCoinbase);
            });
        }

        public void SpendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.SpendTxOutput(chainPosition, txInput, txOutputKey, txOutput, outputScriptHash);
            });
        }

        public void AfterAddTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.AfterAddTransaction(chainPosition, tx);
            });
        }

        public void BeforeRemoveTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.BeforeRemoveTransaction(chainPosition, tx);
            });
        }

        public void UnCoinbaseInput(ChainPosition chainPosition, TxInput txInput)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnCoinbaseInput(chainPosition, txInput);
            });
        }

        public void UnmintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnmintTxOutput(chainPosition, txOutputKey, txOutput, outputScriptHash, isCoinbase);
            });
        }

        public void UnspendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.UnspendTxOutput(chainPosition, txInput, txOutputKey, txOutput, outputScriptHash);
            });
        }

        public void AfterRemoveTransaction(ChainPosition chainPosition, Transaction tx)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.AfterRemoveTransaction(chainPosition, tx);
            });
        }

        public void CommitBlock(ChainedHeader chainedHeader)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.CommitBlock(chainedHeader);
            });
        }

        public void RollbackBlock(ChainedHeader chainedHeader)
        {
            this.actionQueue.Add(() =>
            {
                foreach (var visitor in this.visitors)
                    visitor.RollbackBlock(chainedHeader);
            });
        }

        protected override void WorkAction()
        {
            if (this.actionQueue == null)
                throw new InvalidOperationException();

            foreach (var action in this.actionQueue.GetConsumingEnumerable())
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
