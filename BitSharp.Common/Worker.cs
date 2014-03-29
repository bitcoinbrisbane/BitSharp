using BitSharp.Common.ExtensionMethods;
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

        private readonly CancellationTokenSource shutdownToken;

        private readonly Thread workerThread;
        private readonly AutoResetEvent notifyEvent;
        private readonly AutoResetEvent forceNotifyEvent;
        private readonly ManualResetEventSlim idleEvent;
        private readonly ManualResetEventSlim stopEvent;

        private readonly SemaphoreSlim semaphore;
        private bool isStarted;
        private bool isDisposed;

        public Worker(string name, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
        {
            this.Name = name;
            this.MinIdleTime = minIdleTime;
            this.MaxIdleTime = maxIdleTime;

            this.shutdownToken = new CancellationTokenSource();

            this.workerThread = new Thread(WorkerLoop);
            this.workerThread.Name = "Worker.{0}.WorkerLoop".Format2(name);

            this.notifyEvent = new AutoResetEvent(initialNotify);
            this.forceNotifyEvent = new AutoResetEvent(initialNotify);
            this.idleEvent = new ManualResetEventSlim(false);
            this.stopEvent = new ManualResetEventSlim(true);

            this.semaphore = new SemaphoreSlim(1);
            this.isStarted = false;
            this.isDisposed = false;
        }

        public string Name { get; protected set; }

        public TimeSpan MinIdleTime { get; set; }

        public TimeSpan MaxIdleTime { get; set; }

        protected CancellationTokenSource ShutdownToken { get { return this.shutdownToken; } }

        public void Start()
        {
            if (!this.isStarted)
            {
                this.semaphore.Do(() =>
                {
                    if (!this.isStarted)
                    {
                        this.workerThread.Start();
                        this.isStarted = true;

                        this.SubStart();
                    }
                });
            }

            this.stopEvent.Set();
        }

        public void Stop()
        {
            this.semaphore.Do(() =>
            {
                this.stopEvent.Reset();

                this.SubStop();
            });
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                try
                {
                    this.semaphore.Do(() =>
                    {
                        if (this.isStarted && !this.isDisposed)
                        {
                            this.shutdownToken.Cancel();
                            this.notifyEvent.Set();
                            this.forceNotifyEvent.Set();

                            this.workerThread.Join(5000);

                            this.shutdownToken.Dispose();
                            this.notifyEvent.Dispose();
                            this.forceNotifyEvent.Dispose();
                            this.idleEvent.Dispose();
                            this.stopEvent.Dispose();

                            this.isStarted = false;
                            this.isDisposed = true;

                            this.SubDispose();
                        }
                    });
                    this.semaphore.Dispose();
                }
                catch (Exception) { }
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

                    // stop execution if requested
                    this.stopEvent.Wait();

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

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

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
                    catch (Exception e)
                    {
                        Console.WriteLine(new string('*', 80));
                        Console.WriteLine("Unhandled worker exception: " + e.Message);
                        Console.WriteLine(e);
                        Console.WriteLine(new string('*', 80));
                        throw;
                    }
                    finally
                    {
                        workerTime.Stop();
                    }
                    working = false;

                    stopwatch.Stop();
                    //Debug.WriteLineIf(stopwatch.ElapsedMilliseconds >= 25 && !this.Name.Contains(".StorageWorker"), "{0,35} worked in {1:#,##0.000}s".Format2(this.Name, stopwatch.ElapsedSecondsFloat()));

                    if (DateTime.Now - lastReportTime > TimeSpan.FromSeconds(30))
                    {
                        lastReportTime = DateTime.Now;
                        var percentWorkerTime = workerTime.ElapsedSecondsFloat() / totalTime.ElapsedSecondsFloat();
                        Debug.WriteLineIf(percentWorkerTime > 0.05, "{0,55} work time: {1,10:##0.00%}".Format2(this.Name, percentWorkerTime));
                    }

                    // notify
                    var stopHandler = this.OnWorkStopped;
                    if (stopHandler != null)
                        stopHandler();
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

        protected abstract void WorkAction();

        protected virtual void SubDispose() { }

        protected virtual void SubStart() { }

        protected virtual void SubStop() { }
    }
}
