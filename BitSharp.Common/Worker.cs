using BitSharp.Common.ExtensionMethods;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public abstract class Worker : IDisposable
    {
        private static readonly TimeSpan DEFAULT_STOP_TIMEOUT = TimeSpan.FromSeconds(1);

        public event Action OnNotifyWork;
        public event Action OnWorkStarted;
        public event Action OnWorkStopped;

        private readonly Logger logger;

        private readonly Thread workerThread;
        private readonly ManualResetEventSlim startEvent;
        private readonly AutoResetEvent notifyEvent;
        private readonly AutoResetEvent forceNotifyEvent;
        private readonly ManualResetEventSlim idleEvent;

        private bool isStarted;
        private bool isDisposing;
        private bool isDisposed;

        public Worker(string name, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime, Logger logger)
        {
            this.logger = logger;
            this.Name = name;
            this.MinIdleTime = minIdleTime;
            this.MaxIdleTime = maxIdleTime;

            this.workerThread = new Thread(WorkerLoop);
            this.workerThread.Name = "Worker.{0}.WorkerLoop".Format2(name);

            this.startEvent = new ManualResetEventSlim(false);
            this.notifyEvent = new AutoResetEvent(initialNotify);
            this.forceNotifyEvent = new AutoResetEvent(initialNotify);
            this.idleEvent = new ManualResetEventSlim(false);

            this.isStarted = false;
            this.isDisposing = false;
            this.isDisposed = false;

            this.workerThread.Start();
        }

        public string Name { get; protected set; }

        public TimeSpan MinIdleTime { get; set; }

        public TimeSpan MaxIdleTime { get; set; }

        public bool IsStarted { get { return this.isStarted; } }

        public void Start()
        {
            CheckDisposed();

            if (!this.isStarted)
            {
                this.SubStart();

                this.isStarted = true;
                this.startEvent.Set();
            }
        }

        public void Stop(TimeSpan? timeout = null)
        {
            CheckDisposed();

            if (this.isStarted)
            {
                this.SubStop();

                this.isStarted = false;
                this.startEvent.Reset();

                this.idleEvent.Wait(timeout ?? DEFAULT_STOP_TIMEOUT);
            }
        }

        public void Dispose()
        {
            if (this.isDisposing || this.isDisposed)
                return;

            // stop worker
            this.Stop();

            this.isDisposing = true;

            // subclass dispose
            this.SubDispose();

            // set events so that WorkerLoop() can unblock to discover it is being disposed
            this.startEvent.Set();
            this.notifyEvent.Set();
            this.forceNotifyEvent.Set();

            // wait for worker thread
            if (this.workerThread.IsAlive)
            {
                this.workerThread.Join(DEFAULT_STOP_TIMEOUT);

                // terminate if still alive
                if (this.workerThread.IsAlive)
                    this.workerThread.Abort();
            }

            // dispose events
            this.startEvent.Dispose();
            this.notifyEvent.Dispose();
            this.forceNotifyEvent.Dispose();
            this.idleEvent.Dispose();

            this.isDisposed = true;
        }

        public void NotifyWork()
        {
            CheckDisposed();

            this.notifyEvent.Set();

            var handler = this.OnNotifyWork;
            if (handler != null)
                handler();
        }

        public void ForceWork()
        {
            CheckDisposed();

            this.SubForceWork();

            this.forceNotifyEvent.Set();
            this.notifyEvent.Set();

            var handler = this.OnNotifyWork;
            if (handler != null)
                handler();
        }

        public void WaitForIdle()
        {
            CheckDisposed();

            // wait for worker to idle
            this.idleEvent.Wait();
        }

        public void ForceWorkAndWait()
        {
            CheckDisposed();

            if (!this.isStarted)
                throw new InvalidOperationException();

            // wait for worker to idle
            this.idleEvent.Wait();

            // reset its idle state
            this.idleEvent.Reset();

            // force an execution
            ForceWork();

            // wait for worker to be idle again
            this.idleEvent.Wait();
        }

        public void ThrowIfCancelled()
        {
            CheckDisposed();

            if (!this.isStarted)
                throw new OperationCanceledException();
        }

        private void CheckDisposed()
        {
            if (this.isDisposing || this.isDisposed)
                throw new ObjectDisposedException("Worker");
        }

        private void WorkerLoop()
        {
            try
            {
                var working = false;
                try
                {
                    var totalTime = Stopwatch.StartNew();
                    var workerTime = new Stopwatch();
                    var lastReportTime = DateTime.Now;

                    while (!this.isDisposing)
                    {
                        // notify worker is idle
                        this.idleEvent.Set();

                        // wait for execution to start
                        this.startEvent.Wait();

                        // cooperative loop
                        if (!this.isStarted)
                            continue;

                        // delay for the requested wait time, unless forced
                        this.forceNotifyEvent.WaitOne(this.MinIdleTime);

                        // wait for work notification
                        if (this.MaxIdleTime == TimeSpan.MaxValue)
                            this.notifyEvent.WaitOne();
                        else
                            this.notifyEvent.WaitOne(this.MaxIdleTime - this.MinIdleTime); // subtract time already spent waiting

                        // cooperative loop
                        if (!this.isStarted)
                            continue;

                        // notify that work is starting
                        this.idleEvent.Reset();

                        // notify
                        var startHandler = this.OnWorkStarted;
                        if (startHandler != null)
                            startHandler();

                        // perform the work
                        working = true;
                        workerTime.Start();
                        try
                        {
                            WorkAction();
                        }
                        finally
                        {
                            workerTime.Stop();
                        }
                        working = false;

                        // notify
                        var stopHandler = this.OnWorkStopped;
                        if (stopHandler != null)
                            stopHandler();

                        if (DateTime.Now - lastReportTime > TimeSpan.FromSeconds(30))
                        {
                            lastReportTime = DateTime.Now;
                            var percentWorkerTime = workerTime.ElapsedSecondsFloat() / totalTime.ElapsedSecondsFloat();
                            this.logger.Debug("{0,55} work time: {1,10:##0.00%}".Format2(this.Name, percentWorkerTime));
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // only throw disposed exceptions that occur in WorkAction()
                    if (!this.isDisposed && working)
                        throw;
                }
                catch (OperationCanceledException) { }
            }
            catch (Exception e)
            {
                this.logger.FatalException("Unhandled worker exception", e);
            }
        }

        protected abstract void WorkAction();

        protected virtual void SubDispose() { }

        protected virtual void SubStart() { }

        protected virtual void SubStop() { }

        protected virtual void SubForceWork() { }
    }

    public sealed class WorkerConfig
    {
        public readonly bool initialNotify;
        public readonly TimeSpan minIdleTime;
        public readonly TimeSpan maxIdleTime;

        public WorkerConfig(bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
        {
            this.initialNotify = initialNotify;
            this.minIdleTime = minIdleTime;
            this.maxIdleTime = maxIdleTime;
        }
    }
}
