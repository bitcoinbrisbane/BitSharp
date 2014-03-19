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

        public WorkerMethod(string name, Action workAction, bool runOnStart, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base(name, runOnStart, minIdleTime, maxIdleTime)
        {
            this.workAction = workAction;
        }

        protected override void WorkAction()
        {
            this.workAction();
        }
    }
}
