using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Builders;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using NLog;
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

namespace BitSharp.Core.Workers
{
    internal class DefragWorker : Worker
    {
        private readonly Logger logger;
        private readonly CoreStorage coreStorage;
        private readonly IChainStateCursor chainStateCursor;

        public DefragWorker(WorkerConfig workerConfig, CoreStorage coreStorage, Logger logger)
            : base("DefragWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.coreStorage = coreStorage;
            this.chainStateCursor = coreStorage.OpenChainStateCursor();
        }

        protected override void SubDispose()
        {
            this.chainStateCursor.Dispose();
        }

        protected override void WorkAction()
        {
            this.logger.Info("Begin defragging");

            this.coreStorage.Defragment();
            this.chainStateCursor.Defragment();
        }
    }
}
