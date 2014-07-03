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
    public abstract class BlockingCollectionWorker<T> : IDisposable
    {
        private readonly string name;
        private readonly bool isConcurrent;
        private readonly Logger logger;
        private readonly WorkerMethod[] queueWorkers;
        private readonly bool[] queueWorkersCompleted;
        private readonly object queueWorkersLock;

        private readonly AutoResetEvent workEvent;
        private readonly ManualResetEventSlim completedEvent;
        private ConcurrentQueue<T> queue;
        private bool isStarted;
        private bool isCompleteAdding;
        private bool isCompleted;

        public BlockingCollectionWorker(string name, bool isConcurrent, Logger logger)
        {
            this.name = name;
            this.isConcurrent = isConcurrent;
            this.logger = logger;

            this.workEvent = new AutoResetEvent(false);
            this.completedEvent = new ManualResetEventSlim();

            this.queueWorkers = new WorkerMethod[isConcurrent ? Environment.ProcessorCount * 2 : 1];
            this.queueWorkersCompleted = new bool[this.queueWorkers.Length];
            this.queueWorkersLock = new object();

            for (var i = 0; i < this.queueWorkers.Length; i++)
            {
                this.queueWorkers[i] = new WorkerMethod(name, WorkAction, initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger);
                this.queueWorkers[i].Data = i;
                this.queueWorkers[i].Start();
            }
        }

        public void Dispose()
        {
            this.SubDispose();

            new IDisposable[]
            {
                this.workEvent,
                this.completedEvent
            }.DisposeList();

            this.queueWorkers.DisposeList();
        }

        public string Name { get { return this.name; } }

        public IDisposable Start()
        {
            if (this.isStarted)
                throw new InvalidOperationException();

            this.SubStart();

            this.workEvent.Reset();
            this.completedEvent.Reset();
            this.queue = new ConcurrentQueue<T>();
            this.isStarted = true;
            this.isCompleteAdding = false;
            this.isCompleted = false;

            Array.Clear(this.queueWorkersCompleted, 0, this.queueWorkersCompleted.Length);
            for (var i = 0; i < this.queueWorkers.Length; i++)
            {
                this.queueWorkers[i].NotifyWork();
            }

            return new Stopper(this);
        }

        public void CompleteAdding()
        {
            this.isCompleteAdding = true;
            this.workEvent.Set();
        }

        public void WaitToComplete()
        {
            while (this.isStarted && !this.isCompleted)
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

        protected abstract void CompletedItems();

        private void Stop()
        {
            this.SubStop();

            if (this.isStarted)
            {
                this.CompleteAdding();
                this.WaitToComplete();
            }

            this.isStarted = false;
            this.queue = null;
        }

        private void WorkAction(WorkerMethod instance)
        {
            if (this.queue == null)
                throw new InvalidOperationException();

            foreach (var value in this.GetConsumingEnumerable())
                ConsumeItem(value);

            CompleteWorker((int)instance.Data);
        }

        private void CompleteWorker(int i)
        {
            bool wasCompleted;
            bool completed;
            lock (this.queueWorkersLock)
            {
                wasCompleted = this.queueWorkersCompleted.All(x => x);
                this.queueWorkersCompleted[i] = true;
                completed = this.queueWorkersCompleted.All(x => x);
            }

            if (!wasCompleted && completed)
            {
                Debug.Assert(!this.isCompleted);

                this.CompletedItems();

                this.isCompleted = true;
                this.completedEvent.Set();
            }
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
