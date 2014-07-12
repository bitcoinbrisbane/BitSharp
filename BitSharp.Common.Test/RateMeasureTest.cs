using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common.Test
{
    [TestClass]
    public class RateMeasureTest
    {
        [TestMethod]
        public void TestRateMeasure()
        {
            var sampleCutoff = TimeSpan.FromSeconds(1);
            var sampleResolution = TimeSpan.FromMilliseconds(10);
            using (var rateMeasure = new RateMeasure(sampleCutoff, sampleResolution))
            {
                // tick once per duration, count times
                var count = 10;
                var duration = TimeSpan.FromMilliseconds(20);
                for (var i = 0; i < count; i++)
                {
                    Thread.Sleep(duration);
                    rateMeasure.Tick();
                }

                // the average rate per duration unit time should be as close as possible to 1
                Assert.AreEqual(1, rateMeasure.GetAverage(duration), 0.1);

                // wait for the cutoff time to pass and verify the rate dropped to 0
                Thread.Sleep(duration + rateMeasure.SampleCutoff);
                Assert.AreEqual(0, rateMeasure.GetAverage(duration));
            }
        }
    }
}
