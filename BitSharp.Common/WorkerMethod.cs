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
    public class WorkerMethod : Worker
    {
        private readonly Action workAction;

        public WorkerMethod(string name, Action workAction, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base(name, initialNotify, minIdleTime, maxIdleTime)
        {
            this.workAction = workAction;
        }

        protected override void WorkAction()
        {
            this.workAction();
        }
    }
}
