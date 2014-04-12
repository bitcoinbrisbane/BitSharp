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
    public class ProducerConsumer<T> : IDisposable
    {
        private readonly ConcurrentQueue<T> queue;
        private readonly AutoResetEvent putEvent;
        private bool completedAdding;
        private bool completed;
        private readonly ManualResetEventSlim completedEvent;

        public ProducerConsumer()
        {
            this.queue = new ConcurrentQueue<T>();
            this.putEvent = new AutoResetEvent(false);
            this.completedAdding = false;
            this.completed = false;
            this.completedEvent = new ManualResetEventSlim();
        }

        public void Dispose()
        {
            CompleteAdding();
            this.putEvent.Dispose();
        }

        public bool IsCompleted { get { return this.completed; } }

        public void Add(T value)
        {
            this.queue.Enqueue(value);
            this.putEvent.Set();
        }

        public void CompleteAdding()
        {
            this.completedAdding = true;
            this.putEvent.Set();
        }

        public IEnumerable<T> GetConsumingEnumerable()
        {
            while (true)
            {
                T value;
                while (this.queue.TryDequeue(out value))
                {
                    yield return value;
                }

                if (this.completedAdding && this.queue.Count == 0)
                {
                    this.completed = true;
                    this.completedEvent.Set();
                    yield break;
                }

                this.putEvent.WaitOne(1);
            }
        }

        public void WaitToComplete()
        {
            this.completedEvent.Wait();
        }
    }
}
