using BitSharp.Common.ExtensionMethods;
using NLog;
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
        private readonly Action<WorkerMethod> workAction;

        public WorkerMethod(string name, Action<WorkerMethod> workAction, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime, Logger logger)
            : base(name, initialNotify, minIdleTime, maxIdleTime, logger)
        {
            this.workAction = workAction;
        }

        public object Data { get; set; }

        protected override void WorkAction()
        {
            this.workAction(this);
        }
    }
}
