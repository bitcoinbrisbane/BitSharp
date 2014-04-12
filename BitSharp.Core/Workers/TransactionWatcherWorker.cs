using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Workers
{
    internal class TransactionWatcherWorker : Worker
    {
        public TransactionWatcherWorker(Logger logger)
            : base("TransactionWatcherWorker", initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger)
        {
        }

        public ProducerConsumer<Tuple<int, TxOutput>> ScannerQueue { get; set; }

        public ITransactionMonitor[] TxMonitors { get; set; }

        protected override void WorkAction()
        {
            Parallel.ForEach(
                this.ScannerQueue.GetConsumingEnumerable(),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                txOutput =>
                //foreach (var txOutput in this.ScannerQueue.GetConsumingEnumerable())
                {
                    for (var i = 0; i < this.TxMonitors.Length; i++)
                    {
                        var txMonitor = this.TxMonitors[i];
                        if (txOutput.Item1 < 0)
                            txMonitor.SpendTxOutput(txOutput.Item2);
                        else
                            txMonitor.MintTxOutput(txOutput.Item2);
                    }
                });
        }
    }
}
