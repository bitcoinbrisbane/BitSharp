using BitSharp.Common.ExtensionMethods;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class ParallelConsumer<T> : IDisposable
    {
        private readonly string name;
        private readonly Logger logger;

        private readonly WorkerMethod readWorker;
        private readonly WorkerMethod consumeWorker;
        private ManualResetEventSlim completedReadingEvent = new ManualResetEventSlim(false);
        private ManualResetEventSlim completedEvent = new ManualResetEventSlim(false);

        private IEnumerable<T> source;
        private Action<T> consumeAction;
        private Action completedAction;
        private BlockingQueue<T> queue;

        private bool isStarted;

        public ParallelConsumer(string name, Logger logger)
        {
            this.name = name;
            this.logger = logger;

            this.readWorker = new WorkerMethod(name + ".ReadWorker", ReadWorker, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger);
            this.consumeWorker = new WorkerMethod(name + ".ConsumeWorker", ConsumeWorker, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger);

            this.readWorker.Start();
            this.consumeWorker.Start();
        }

        public void Dispose()
        {
            this.Stop();

            new IDisposable[]
            {
                this.readWorker,
                this.consumeWorker,
                this.completedReadingEvent,
                this.completedEvent,
                this.queue
            }.DisposeList();
        }

        public string Name { get { return this.name; } }

        public IDisposable Start(IEnumerable<T> source, Action<T> consumeAction, Action completedAction)
        {
            if (this.isStarted)
                throw new InvalidOperationException();

            this.source = source;
            this.consumeAction = consumeAction;
            this.completedAction = completedAction;
            this.queue = new BlockingQueue<T>();

            this.completedReadingEvent.Reset();
            this.completedEvent.Reset();
            this.isStarted = true;

            this.readWorker.NotifyWork();
            this.consumeWorker.NotifyWork();

            return new Stopper(this);
        }

        public void WaitToComplete()
        {
            if (!this.isStarted)
                throw new InvalidOperationException();

            this.completedReadingEvent.Wait();
            this.completedEvent.Wait();
        }

        private void Stop()
        {
            if (!this.isStarted)
                return;

            this.WaitToComplete();

            this.queue.Dispose();
            this.source = null;
            this.consumeAction = null;
            this.completedAction = null;
            this.queue = null;

            this.isStarted = false;
        }

        private void ReadWorker(WorkerMethod instance)
        {
            try
            {
                foreach (var item in this.source)
                {
                    this.queue.Add(item);
                }
            }
            finally
            {
                this.queue.CompleteAdding();
                this.completedReadingEvent.Set();
            }
        }

        private void ConsumeWorker(WorkerMethod instance)
        {
            try
            {
                Parallel.ForEach(this.queue.GetConsumingEnumerable(),
                    value => this.consumeAction(value));
            }
            finally
            {
                this.completedAction();
                this.completedEvent.Set();
            }
        }

        private sealed class Stopper : IDisposable
        {
            private readonly ParallelConsumer<T> worker;

            public Stopper(ParallelConsumer<T> worker)
            {
                this.worker = worker;
            }

            public void Dispose()
            {
                this.worker.Stop();
            }
        }
    }
}
