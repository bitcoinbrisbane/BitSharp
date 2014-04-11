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
    public class LookAheadTest
    {
        [TestMethod]
        public void TestReadException()
        {
            var expectedException = new Exception();
            try
            {
                foreach (var value in this.BadEnumerable(expectedException).LookAhead(1))
                {
                }

                Assert.Fail("Expected exeption.");
            }
            catch (Exception actualException)
            {
                Assert.AreSame(expectedException, actualException);
            }
        }

        private IEnumerable<object> BadEnumerable(Exception e)
        {
            yield return new Object();
            yield return new Object();
            yield return new Object();
            yield return new Object();
            throw e;
        }
    }
}
