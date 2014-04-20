using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public abstract class BlockingCollectionWorker<T> : IDisposable
    {
        private readonly string name;
        private readonly bool isConcurrent;
        private readonly Logger logger;
        private readonly WorkerMethod queueWorker;

        private AutoResetEvent workEvent;
        private ManualResetEventSlim completedEvent;
        private ConcurrentQueue<T> queue;
        private bool isCompleteAdding;
        private bool isCompleted;

        public BlockingCollectionWorker(string name, bool isConcurrent, Logger logger)
        {
            this.name = name;
            this.isConcurrent = isConcurrent;
            this.logger = logger;
            this.queueWorker = new WorkerMethod(name, WorkAction, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger);
            this.queueWorker.Start();
        }

        public void Dispose()
        {
            this.SubDispose();
            this.queueWorker.Dispose();
        }

        public string Name { get { return this.name; } }

        public IDisposable Start()
        {
            if (this.queue != null)
                throw new InvalidOperationException();

            this.SubStart();

            this.workEvent = new AutoResetEvent(false);
            this.completedEvent = new ManualResetEventSlim();
            this.queue = new ConcurrentQueue<T>();
            this.isCompleteAdding = false;
            this.isCompleted = false;

            this.queueWorker.NotifyWork();
            return new Stopper(this);
        }

        public void CompleteAdding()
        {
            if (this.queue == null)
                throw new InvalidOperationException();

            this.isCompleteAdding = true;
            this.workEvent.Set();
        }

        public void WaitToComplete()
        {
            if (this.queue == null)
                throw new InvalidOperationException();

            while (!this.isCompleted)
                this.completedEvent.Wait(1);
        }

        public void Add(T value)
        {
            if (this.queue == null || this.isCompleteAdding)
                throw new InvalidOperationException();

            this.queue.Enqueue(value);
            this.workEvent.Set();
        }

        protected virtual void SubDispose() { }

        protected virtual void SubStart() { }

        protected virtual void SubStop() { }

        protected abstract void ConsumeItem(T value);

        private void Stop()
        {
            if (this.queue == null || !this.isCompleteAdding || !this.isCompleted)
                throw new InvalidOperationException();

            this.SubStop();

            this.workEvent.Dispose();
            this.completedEvent.Dispose();
            this.queue = null;
        }

        private void WorkAction()
        {
            if (this.queue == null)
                throw new InvalidOperationException();

            if (this.isConcurrent)
            {
                Parallel.ForEach(
                    this.GetConsumingEnumerable(),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                    value => ConsumeItem(value));
            }
            else
            {
                foreach (var value in this.GetConsumingEnumerable())
                    ConsumeItem(value);
            }

            this.isCompleted = true;
            this.completedEvent.Set();
        }

        private IEnumerable<T> GetConsumingEnumerable()
        {
            while (true)
            {
                T value;
                while (this.queue.TryDequeue(out value))
                {
                    if (this.queue.Count > 0)
                        this.workEvent.Set();

                    yield return value;
                }

                if (this.isCompleteAdding && this.queue.Count == 0)
                {
                    this.workEvent.Set();
                    yield break;
                }

                this.workEvent.WaitOne(1);
            }
        }

        private sealed class Stopper : IDisposable
        {
            private readonly BlockingCollectionWorker<T> worker;

            public Stopper(BlockingCollectionWorker<T> worker)
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
