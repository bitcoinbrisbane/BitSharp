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
        private readonly Action workAction;

        public WorkerMethod(string name, Action workAction, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime, Logger logger)
            : base(name, initialNotify, minIdleTime, maxIdleTime, logger)
        {
            this.workAction = workAction;
        }

        protected override void WorkAction()
        {
            this.workAction();
        }
    }
}
