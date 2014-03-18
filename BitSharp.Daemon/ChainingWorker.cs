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
    public class ChainingWorker : IDisposable
    {
        private readonly CancellationTokenSource shutdownToken;
        private readonly IBlockchainRules rules;
        private readonly CacheContext cacheContext;
        private readonly ChainingCalculator chainingCalculator;
        private readonly Worker worker;

        public ChainingWorker(IBlockchainRules rules, CacheContext cacheContext)
        {
            this.shutdownToken = new CancellationTokenSource();

            this.rules = rules;
            this.cacheContext = cacheContext;

            this.chainingCalculator = new ChainingCalculator(cacheContext);

            // create workers
            this.worker = new Worker("ChainingWorker.WorkerThread", WorkerThread,
                runOnStart: true, waitTime: TimeSpan.FromSeconds(0), maxIdleTime: TimeSpan.FromSeconds(30));

            this.chainingCalculator.OnQueued += NotifyWork;
            this.chainingCalculator.QueueAllBlockHeaders();
        }

        public void Start()
        {
            try
            {
                // start loading the existing state from storage
                //TODO LoadExistingState();

                // startup workers
                this.worker.Start();
            }
            catch (Exception)
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            // unwire events
            this.chainingCalculator.OnQueued -= NotifyWork;

            // notify threads to begin shutting down
            this.shutdownToken.Cancel();

            // cleanup workers
            new IDisposable[]
            {
                this.chainingCalculator,
                this.worker,
                this.shutdownToken
            }.DisposeList();
        }

        private void NotifyWork()
        {
            this.worker.NotifyWork();
        }

        private void WorkerThread()
        {
            this.chainingCalculator.ChainBlockHeaders(this.shutdownToken.Token);
        }
    }
}
