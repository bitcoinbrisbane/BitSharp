using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class DurationMeasure : IDisposable
    {
        private readonly Func<DateTime> getDateTime;
        private readonly ReaderWriterLockSlim rwLock;

        private bool isDisposed;
        private readonly DateTime startTime;
        private List<Sample> samples;
        private int tickCount;
        private long tickDuration;

        public DurationMeasure()
            : this(() => DateTime.UtcNow)
        { }

        internal DurationMeasure(Func<DateTime> getDateTime)
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

                var start = this.samples[0].Start;
                var now = this.getDateTime();
                
                var duration = now - start;
                if (duration == TimeSpan.Zero)
                    return TimeSpan.Zero;

                var totalTickCount = this.samples.Sum(x => x.TickCount);
                if (totalTickCount == 0)
                    return TimeSpan.Zero;

                var totalTickDuration = this.samples.Sum(x => x.TickDuration);

                return new TimeSpan(totalTickDuration / totalTickCount);
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
                var tickCountLocal = Interlocked.Exchange(ref this.tickCount, 0);
                var tickDurationLocal = Interlocked.Exchange(ref this.tickDuration, 0);

                this.rwLock.DoWrite(() =>
                {
                    while (this.samples.Count > 0 && this.samples[0].Start < cutoff)
                        this.samples.RemoveAt(0);
                    this.samples.Add(new Sample { Start = now, Length = duration, TickCount = tickCountLocal, TickDuration = tickDurationLocal });
                });
            }
        }

        private sealed class Sample
        {
            public DateTime Start { get; set; }
            public TimeSpan Length { get; set; }
            public int TickCount { get; set; }
            public long TickDuration { get; set; }
        }
    }
}
