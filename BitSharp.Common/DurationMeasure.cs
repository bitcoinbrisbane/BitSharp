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
    public class DurationMeasure : IDisposable
    {
        private readonly ReaderWriterLockSlim rwLock;

        private readonly CancellationTokenSource cancelToken;
        private readonly Stopwatch stopwatch;
        private List<Sample> samples;
        private int tickCount;
        private long tickDuration;
        private readonly Task sampleTask;
        private bool isDisposed;

        public DurationMeasure(TimeSpan? sampleCutoff = null, TimeSpan? sampleResolution = null)
        {
            this.rwLock = new ReaderWriterLockSlim();
            this.cancelToken = new CancellationTokenSource();
            this.stopwatch = Stopwatch.StartNew();
            this.samples = new List<Sample>();

            this.SampleCutoff = sampleCutoff ?? TimeSpan.FromSeconds(30);
            this.SampleResolution = sampleResolution ?? TimeSpan.FromSeconds(1);

            this.sampleTask = Task.Run((Func<Task>)this.SampleThread);
        }

        public TimeSpan SampleCutoff { get; set; }

        public TimeSpan SampleResolution { get; set; }

        public void Dispose()
        {
            if (this.isDisposed)
                return;

            this.cancelToken.Cancel();
            this.sampleTask.Wait();

            this.cancelToken.Dispose();
            this.rwLock.Dispose();

            this.isDisposed = true;
        }

        public void Tick(TimeSpan duration)
        {
            Interlocked.Increment(ref this.tickCount);
            Interlocked.Add(ref this.tickDuration, duration.Ticks);
        }

        public TimeSpan GetAverage()
        {
            return this.rwLock.DoRead(() =>
            {
                if (this.samples.Count == 0)
                    return TimeSpan.Zero;

                var start = this.samples[0].SampleStart;
                var now = stopwatch.Elapsed;

                var duration = now - start;
                if (duration <= TimeSpan.Zero)
                    return TimeSpan.Zero;

                var totalTickCount = this.samples.Sum(x => x.TickCount) + this.tickCount;
                if (totalTickCount == 0)
                    return TimeSpan.Zero;

                var totalTickDuration = this.samples.Sum(x => x.TickDuration) + this.tickDuration;

                return new TimeSpan(totalTickDuration / totalTickCount);
            });
        }

        private async Task SampleThread()
        {
            while (true)
            {
                var start = stopwatch.Elapsed;

                try
                {
                    await Task.Delay(this.SampleResolution, this.cancelToken.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                var now = stopwatch.Elapsed;
                var duration = now - start;
                var cutoff = now - this.SampleCutoff;

                this.rwLock.DoWrite(() =>
                {
                    var tickCountLocal = Interlocked.Exchange(ref this.tickCount, 0);
                    var tickDurationLocal = Interlocked.Exchange(ref this.tickDuration, 0);

                    while (this.samples.Count > 0 && this.samples[0].SampleStart < cutoff)
                        this.samples.RemoveAt(0);
                    this.samples.Add(new Sample { SampleStart = start, SampleDuration = duration, TickCount = tickCountLocal, TickDuration = tickDurationLocal });
                });
            }
        }

        private sealed class Sample
        {
            public TimeSpan SampleStart { get; set; }
            public TimeSpan SampleDuration { get; set; }
            public int TickCount { get; set; }
            public long TickDuration { get; set; }
        }
    }
}
