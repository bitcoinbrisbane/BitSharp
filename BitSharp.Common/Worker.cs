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
        public event Action OnNotifyWork;
        public event Action OnWorkStarted;
        public event Action OnWorkStopped;

        private readonly Logger logger;
        private readonly CancellationTokenSource shutdownToken;

        private readonly Thread workerThread;
        private readonly AutoResetEvent notifyEvent;
        private readonly AutoResetEvent forceNotifyEvent;
        private readonly ManualResetEventSlim idleEvent;

        private SemaphoreSlim semaphore;
        private readonly ManualResetEventSlim startedEvent;
        private bool isDisposed;

        public Worker(string name, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime, Logger logger)
        {
            this.logger = logger;
            this.Name = name;
            this.MinIdleTime = minIdleTime;
            this.MaxIdleTime = maxIdleTime;

            this.shutdownToken = new CancellationTokenSource();

            this.workerThread = new Thread(WorkerLoop);
            this.workerThread.Name = "Worker.{0}.WorkerLoop".Format2(name);

            this.notifyEvent = new AutoResetEvent(initialNotify);
            this.forceNotifyEvent = new AutoResetEvent(initialNotify);
            this.idleEvent = new ManualResetEventSlim(false);

            this.semaphore = new SemaphoreSlim(1);
            this.startedEvent = new ManualResetEventSlim(false);
            this.isDisposed = false;
        }

        public string Name { get; protected set; }

        public TimeSpan MinIdleTime { get; set; }

        public TimeSpan MaxIdleTime { get; set; }

        protected CancellationTokenSource ShutdownToken { get { return this.shutdownToken; } }

        public void Start()
        {
            this.semaphore.Do(() =>
            {
                if (!this.startedEvent.IsSet)
                {
                    this.SubStart();

                    this.workerThread.Start();
                    this.startedEvent.Set();
                }
            });
        }

        public void Stop()
        {
            this.semaphore.Do(() =>
            {
                if (this.startedEvent.IsSet)
                {
                    this.SubStop();

                    this.startedEvent.Reset();
                }
            });
        }

        public void Dispose()
        {
            var semaphoreLocal = this.semaphore;
            if (semaphoreLocal != null)
            {
                var wasDisposed = false;
                semaphoreLocal.Do(() =>
                {
                    if (!this.isDisposed)
                    {
                        this.SubDispose();

                        this.shutdownToken.Cancel();
                        this.notifyEvent.Set();
                        this.forceNotifyEvent.Set();

                        if (this.workerThread.IsAlive)
                            this.workerThread.Join(5000);

                        this.shutdownToken.Dispose();
                        this.notifyEvent.Dispose();
                        this.forceNotifyEvent.Dispose();
                        this.idleEvent.Dispose();
                        this.startedEvent.Dispose();

                        this.semaphore = null;
                        this.isDisposed = true;
                        wasDisposed = true;
                    }
                });

                if (wasDisposed)
                    semaphoreLocal.Dispose();
            }
        }

        public void NotifyWork()
        {
            if (this.isDisposed)
                return;

            this.notifyEvent.Set();

            var handler = this.OnNotifyWork;
            if (handler != null)
                handler();
        }

        public void ForceWork()
        {
            if (this.isDisposed)
                return;

            this.forceNotifyEvent.Set();
            this.notifyEvent.Set();

            var handler = this.OnNotifyWork;
            if (handler != null)
                handler();
        }

        public void ForceWorkAndWait()
        {
            if (this.isDisposed)
                return;
            if (!this.startedEvent.IsSet)
                return;

            // wait for worker to idle
            this.idleEvent.Wait();

            // reset its idle state
            this.idleEvent.Reset();

            // force an execution
            ForceWork();

            // wait for worker to be idle again
            this.idleEvent.Wait();
        }

        private void WorkerLoop()
        {
            try
            {
                var working = false;
                try
                {
                    var totalTime = new Stopwatch();
                    var workerTime = new Stopwatch();
                    var lastReportTime = DateTime.Now;

                    totalTime.Start();

                    while (true)
                    {
                        // cooperative loop
                        this.shutdownToken.Token.ThrowIfCancellationRequested();

                        // notify worker is idle
                        this.idleEvent.Set();

                        // wait for execution to start
                        this.startedEvent.Wait();

                        // delay for the requested wait time, unless forced
                        this.forceNotifyEvent.WaitOne(this.MinIdleTime);

                        // wait for work notification
                        if (this.MaxIdleTime == TimeSpan.MaxValue)
                            this.notifyEvent.WaitOne();
                        else
                            this.notifyEvent.WaitOne(this.MaxIdleTime - this.MinIdleTime); // subtract time already spent waiting

                        // cooperative loop
                        this.shutdownToken.Token.ThrowIfCancellationRequested();

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
