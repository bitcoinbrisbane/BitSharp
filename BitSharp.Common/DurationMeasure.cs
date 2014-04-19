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

        private bool isDisposed;
        private List<Sample> samples;
        private int tickCount;
        private long tickDuration;

        public DurationMeasure()
        {
            this.rwLock = new ReaderWriterLockSlim();
            this.samples = new List<Sample>();

            this.SampleTimeSpan = TimeSpan.FromSeconds(30);
            this.Resolution = TimeSpan.FromSeconds(1);

            new Thread(SampleThread).Start();
        }

        public TimeSpan Resolution { get; set; }

        public TimeSpan SampleTimeSpan { get; set; }

        public void Dispose()
        {
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
                var now = Stopwatch.GetTimestamp();

                var duration = now - start;
                if (duration <= 0)
                    return TimeSpan.Zero;

                var totalTickCount = this.samples.Sum(x => x.TickCount) + this.tickCount;
                if (totalTickCount == 0)
                    return TimeSpan.Zero;

                var totalTickDuration = this.samples.Sum(x => x.TickDuration) + this.tickDuration;

                return new TimeSpan(totalTickDuration / totalTickCount);
            });
        }

        private void SampleThread()
        {
            while (!this.isDisposed)
            {
                var start = Stopwatch.GetTimestamp();
                Thread.Sleep(this.Resolution);

                var now = Stopwatch.GetTimestamp();
                var duration = now - start;
                var cutoff = now - this.SampleTimeSpan.Ticks;

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
            public long SampleStart { get; set; }
            public long SampleDuration { get; set; }
            public int TickCount { get; set; }
            public long TickDuration { get; set; }
        }
    }
}
