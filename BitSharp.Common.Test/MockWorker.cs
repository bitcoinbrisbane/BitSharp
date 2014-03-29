using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common.Test
{
    public class MockWorker : Worker
    {
        private readonly Action workAction;
        private readonly Action subDispose;
        private readonly Action subStart;
        private readonly Action subStop;

        public MockWorker(String name, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime, Action workAction, Action subDispose = null, Action subStart = null, Action subStop = null)
            : base(name, initialNotify, minIdleTime, maxIdleTime)
        {
            if (workAction == null)
                throw new ArgumentException("workAction");

            this.workAction = workAction;
            this.subDispose = subDispose;
            this.subStart = subStart;
            this.subStop = subStop;
        }

        protected override void WorkAction()
        {
            this.workAction();
        }

        protected override void SubDispose()
        {
            if (this.subDispose != null)
                this.subDispose();
        }

        protected override void SubStart()
        {
            if (this.subStart != null)
                this.subStart();
        }
        
        protected override void SubStop()
        {
            if (this.subStop != null)
                this.subStop();
        }
    }
}
