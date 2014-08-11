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
        private ManualResetEventSlim completedReadingEvent = new ManualResetEventSlim(false);

        private readonly WorkerMethod[] consumeWorkers;
        private readonly bool[] consumeWorkersCompleted;
        private readonly object consumeWorkersLock;

        private readonly ManualResetEventSlim completedEvent = new ManualResetEventSlim(false);

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
            this.readWorker.Start();

            this.consumeWorkers = new WorkerMethod[Environment.ProcessorCount * 2];
            this.consumeWorkersCompleted = new bool[this.consumeWorkers.Length];
            this.consumeWorkersLock = new object();

            for (var i = 0; i < this.consumeWorkers.Length; i++)
            {
                this.consumeWorkers[i] = new WorkerMethod(name + ".ConsumeWorker." + i, ConsumeWorker, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger);
                this.consumeWorkers[i].Data = i;
                this.consumeWorkers[i].Start();
            }
        }

        public void Dispose()
        {
            this.Stop();

            this.consumeWorkers.DisposeList();

            new IDisposable[]
            {
                this.readWorker,
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

            Array.Clear(this.consumeWorkersCompleted, 0, this.consumeWorkersCompleted.Length);
            for (var i = 0; i < this.consumeWorkers.Length; i++)
            {
                this.consumeWorkers[i].NotifyWork();
            }

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
                foreach (var value in this.queue.GetConsumingEnumerable())
                    this.consumeAction(value);
            }
            finally
            {
                CompleteWorker((int)instance.Data);
            }
        }

        private void CompleteWorker(int i)
        {
            bool wasCompleted;
            bool completed;
            lock (this.consumeWorkersLock)
            {
                wasCompleted = this.consumeWorkersCompleted.All(x => x);
                this.consumeWorkersCompleted[i] = true;
                completed = this.consumeWorkersCompleted.All(x => x);
            }

            if (!wasCompleted && completed)
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
