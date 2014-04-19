using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class RateMeasure : IDisposable
    {
        private readonly Func<DateTime> getDateTime;
        private readonly ReaderWriterLockSlim rwLock;

        private bool isDisposed;
        private readonly DateTime startTime;
        private List<Sample> samples;
        private int tickCount;

        public RateMeasure()
            : this(() => DateTime.UtcNow)
        { }

        internal RateMeasure(Func<DateTime> getDateTime)
        {
            this.getDateTime = getDateTime;
            this.rwLock = new ReaderWriterLockSlim();
            this.samples = new List<Sample>();

            this.SampleTimeSpan = TimeSpan.FromSeconds(30);
            this.Resolution = TimeSpan.FromSeconds(1);

            new Thread(AverageThread).Start();
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

                var start = this.samples[0].Start;
                var now = this.getDateTime();

                var duration = now - start;
                if (duration == TimeSpan.Zero)
                    return float.NaN;

                var totalTickCount = this.samples.Sum(x => x.TickCount) + this.tickCount;

                var unitsOfTime = (float)duration.Ticks / perUnitTime.Ticks;

                return totalTickCount / unitsOfTime;
            });
        }

        private void AverageThread()
        {
            while (!this.isDisposed)
            {
                var duration = this.Resolution;
                Thread.Sleep(duration);

                var now = this.getDateTime();
                var cutoff = now - this.SampleTimeSpan;

                this.rwLock.DoWrite(() =>
                {
                    var tickCountLocal = Interlocked.Exchange(ref this.tickCount, 0);

                    while (this.samples.Count > 0 && this.samples[0].Start < cutoff)
                        this.samples.RemoveAt(0);
                    this.samples.Add(new Sample { Start = now, Length = duration, TickCount = tickCountLocal });
                });
            }
        }

        private sealed class Sample
        {
            public DateTime Start { get; set; }
            public TimeSpan Length { get; set; }
            public int TickCount { get; set; }
        }
    }
}
