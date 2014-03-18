using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Daemon
{
    public class ChainingWorker : Worker
    {
        private readonly IBlockchainRules rules;
        private readonly CacheContext cacheContext;
        private readonly ChainingCalculator chainingCalculator;

        public ChainingWorker(IBlockchainRules rules, CacheContext cacheContext)
            : base("ChainingWorker", initialNotify: true, waitTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30))
        {
            this.rules = rules;
            this.cacheContext = cacheContext;

            this.chainingCalculator = new ChainingCalculator(cacheContext);

            this.chainingCalculator.OnQueued += NotifyWork;
            this.chainingCalculator.QueueAllBlockHeaders();
        }

        protected override void SubDispose()
        {
            // unwire events
            this.chainingCalculator.OnQueued -= NotifyWork;

            // cleanup workers
            this.chainingCalculator.Dispose();
        }

        protected override void WorkAction()
        {
            this.chainingCalculator.ChainBlockHeaders(this.ShutdownToken.Token);
        }
    }
}
