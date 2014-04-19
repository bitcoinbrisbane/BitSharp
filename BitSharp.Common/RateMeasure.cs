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
    public class RateMeasure : IDisposable
    {
        private readonly ReaderWriterLockSlim rwLock;

        private bool isDisposed;
        private List<Sample> samples;
        private int tickCount;

        public RateMeasure()
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

        public void Tick()
        {
            Interlocked.Increment(ref this.tickCount);
        }

        public float GetAverage(TimeSpan perUnitTime)
        {
            return this.rwLock.DoRead(() =>
            {
                if (this.samples.Count == 0)
                    return float.NaN;

                var start = this.samples[0].SampleStart;
                var now = Stopwatch.GetTimestamp();

                var duration = now - start;
                if (duration <= 0)
                    return float.NaN;

                var totalTickCount = this.samples.Sum(x => x.TickCount) + this.tickCount;

                var unitsOfTime = (float)duration / perUnitTime.Ticks;

                return totalTickCount / unitsOfTime;
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

                    while (this.samples.Count > 0 && this.samples[0].SampleStart < cutoff)
                        this.samples.RemoveAt(0);
                    this.samples.Add(new Sample { SampleStart = start, SampleDuration = duration, TickCount = tickCountLocal });
                });
            }
        }

        private sealed class Sample
        {
            public long SampleStart { get; set; }
            public long SampleDuration { get; set; }
            public int TickCount { get; set; }
        }
    }
}
