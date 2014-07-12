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
    public class CountMeasure : IDisposable
    {
        private readonly ReaderWriterLockSlim rwLock;

        private readonly ManualResetEventSlim stopToken;
        private readonly Stopwatch stopwatch;
        private List<Sample> samples;
        private int tickCount;
        private Thread sampleThread;

        public CountMeasure(TimeSpan sampleCutoff, TimeSpan? sampleResolution = null)
        {
            this.rwLock = new ReaderWriterLockSlim();
            this.stopToken = new ManualResetEventSlim();
            this.stopwatch = Stopwatch.StartNew();
            this.samples = new List<Sample> { new Sample { SampleStart = this.stopwatch.Elapsed, TickCount = 0 } };

            this.SampleCutoff = sampleCutoff;
            this.SampleResolution = sampleResolution ?? TimeSpan.FromSeconds(1);

            this.sampleThread = new Thread(SampleThread);
            this.sampleThread.Start();
        }

        public TimeSpan SampleCutoff { get; set; }

        public TimeSpan SampleResolution { get; set; }

        public void Dispose()
        {
            this.stopToken.Set();
            this.sampleThread.Join();
            this.stopToken.Dispose();
        }

        public void Tick()
        {
            Interlocked.Increment(ref this.tickCount);
        }

        public void Tick(int count)
        {
            Interlocked.Add(ref this.tickCount, count);
        }

        public int GetCount()
        {
            return this.rwLock.DoRead(() =>
            {
                if (this.samples.Count == 0)
                    return 0;

                var start = this.samples[0].SampleStart;
                var now = stopwatch.Elapsed;

                var totalTickCount = this.samples.Sum(x => x.TickCount) + this.tickCount;

                return totalTickCount;
            });
        }

        private void SampleThread()
        {
            while (true)
            {
                var start = stopwatch.Elapsed;

                if (this.stopToken.Wait(this.SampleResolution))
                    return;

                var now = stopwatch.Elapsed;
                var cutoff = now - this.SampleCutoff;

                this.rwLock.DoWrite(() =>
                {
                    var tickCountLocal = Interlocked.Exchange(ref this.tickCount, 0);

                    while (this.samples.Count > 0 && this.samples[0].SampleStart < cutoff)
                        this.samples.RemoveAt(0);
                    this.samples.Add(new Sample { SampleStart = start, TickCount = tickCountLocal });
                });
            }
        }

        private sealed class Sample
        {
            public TimeSpan SampleStart { get; set; }
            public int TickCount { get; set; }
        }
    }
}
