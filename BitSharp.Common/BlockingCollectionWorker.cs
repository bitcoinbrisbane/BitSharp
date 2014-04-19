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
        private readonly WorkerMethod queueWorker;

        private ManualResetEventSlim completedEvent;
        private BlockingCollection<T> queue;

        public BlockingCollectionWorker(string name, bool isConcurrent, Logger logger)
        {
            this.name = name;
            this.isConcurrent = isConcurrent;
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

            this.completedEvent = new ManualResetEventSlim();
            this.queue = new BlockingCollection<T>();
            this.queueWorker.NotifyWork();

            return new Stopper(this);
        }

        public void CompleteAdding()
        {
            if (this.queue == null)
                throw new InvalidOperationException();

            this.queue.CompleteAdding();
        }

        public void WaitToComplete()
        {
            if (this.queue == null)
                throw new InvalidOperationException();

            this.completedEvent.Wait();
        }

        public void Add(T value)
        {
            if (this.queue == null)
                throw new InvalidOperationException();

            this.queue.Add(value);
        }

        protected virtual void SubDispose() { }

        protected virtual void SubStart() { }

        protected virtual void SubStop() { }

        protected abstract void ConsumeItem(T value);

        private void Stop()
        {
            if (this.queue == null || !this.queue.IsCompleted)
                throw new InvalidOperationException();

            this.SubStop();

            this.queue.Dispose();
            this.queue = null;
        }

        private void WorkAction()
        {
            if (this.queue == null)
                throw new InvalidOperationException();

            if (this.isConcurrent)
            {
                Parallel.ForEach(
                    this.queue.GetConsumingEnumerable(),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                    value => ConsumeItem(value));
            }
            else
            {
                foreach (var value in this.queue.GetConsumingEnumerable())
                    ConsumeItem(value);
            }

            this.completedEvent.Set();
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
