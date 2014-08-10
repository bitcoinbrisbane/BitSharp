using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class BlockingQueue<T> : IDisposable
    {
        private readonly ConcurrentQueue<T> queue = new ConcurrentQueue<T>();
        private readonly AutoResetEvent workEvent = new AutoResetEvent(false);

        private bool isCompleteAdding;

        public void Dispose()
        {
            this.workEvent.Dispose();
        }

        public void Add(T value)
        {
            if (this.queue == null || this.isCompleteAdding)
                throw new InvalidOperationException();

            this.queue.Enqueue(value);
            this.workEvent.Set();
        }

        public void CompleteAdding()
        {
            this.isCompleteAdding = true;
            this.workEvent.Set();
        }

        public IEnumerable<T> GetConsumingEnumerable()
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

                this.workEvent.WaitOne();
            }
        }
    }
}
