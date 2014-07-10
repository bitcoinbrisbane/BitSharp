using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Node.Network;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Node.Workers
{
    public class HeadersRequestWorker : Worker
    {
        private static readonly TimeSpan STALE_REQUEST_TIME = TimeSpan.FromSeconds(60);

        private readonly Logger logger;
        private readonly LocalClient localClient;
        private readonly CoreDaemon coreDaemon;
        private readonly CoreStorage coreStorage;

        private readonly ConcurrentDictionary<IPEndPoint, DateTime> headersRequestsByPeer;

        private readonly WorkerMethod flushWorker;
        private readonly ConcurrentQueue<Tuple<RemoteNode, IImmutableList<BlockHeader>>> flushQueue;

        public HeadersRequestWorker(WorkerConfig workerConfig, Logger logger, LocalClient localClient, CoreDaemon coreDaemon)
            : base("HeadersRequestWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.localClient = localClient;
            this.coreDaemon = coreDaemon;
            this.coreStorage = coreDaemon.CoreStorage;

            this.headersRequestsByPeer = new ConcurrentDictionary<IPEndPoint, DateTime>();

            this.localClient.OnBlockHeaders += HandleBlockHeaders;
            this.coreDaemon.OnTargetChainChanged += HandleTargetChainChanged;

            this.flushWorker = new WorkerMethod("HeadersRequestWorker.FlushWorker", FlushWorkerMethod, initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: this.logger);
            this.flushQueue = new ConcurrentQueue<Tuple<RemoteNode, IImmutableList<BlockHeader>>>();
        }

        protected override void SubDispose()
        {
            this.localClient.OnBlockHeaders -= HandleBlockHeaders;
            this.coreDaemon.OnTargetChainChanged -= HandleTargetChainChanged;

            this.flushWorker.Dispose();
        }

        protected override void SubStart()
        {
            this.flushWorker.Start();
        }

        protected override void SubStop()
        {
            this.flushWorker.Stop();
        }

        protected override void WorkAction()
        {
            var now = DateTime.UtcNow;
            var requestTasks = new List<Task>();

            var peerCount = this.localClient.ConnectedPeers.Count;
            if (peerCount == 0)
                return;

            var targetChainLocal = this.coreDaemon.TargetChain;
            if (targetChainLocal == null)
                return;

            var blockLocatorHashes = CalculateBlockLocatorHashes(targetChainLocal.Blocks);

            // remove any stale requests from the peer's list of requests
            this.headersRequestsByPeer.RemoveWhere(x => (now - x.Value) > STALE_REQUEST_TIME);

            // loop through each connected peer
            foreach (var peer in this.localClient.ConnectedPeers)
            {
                // determine if a new request can be made
                if (this.headersRequestsByPeer.TryAdd(peer.Key, now))
                {
                    // send out the request for headers
                    requestTasks.Add(peer.Value.Sender.SendGetHeaders(blockLocatorHashes, hashStop: 0));
                    
                    // only send out a single header request at a time
                    break;
                }
            }
        }

        private void FlushWorkerMethod(WorkerMethod instance)
        {
            Tuple<RemoteNode, IImmutableList<BlockHeader>> tuple;
            while (this.flushQueue.TryDequeue(out tuple))
            {
                // cooperative loop
                this.ThrowIfCancelled();

                var remoteNode = tuple.Item1;
                var blockHeaders = tuple.Item2;

                foreach (var blockHeader in blockHeaders)
                {
                    // cooperative loop
                    this.ThrowIfCancelled();
                    
                    ChainedHeader chainedHeader;
                    this.coreStorage.TryChainHeader(blockHeader, out chainedHeader);
                }

                DateTime ignore;
                this.headersRequestsByPeer.TryRemove(remoteNode.RemoteEndPoint, out ignore);
            }
        }

        private void HandleBlockHeaders(RemoteNode remoteNode, IImmutableList<BlockHeader> blockHeaders)
        {
            if (blockHeaders.Count > 0)
            {
                this.flushQueue.Enqueue(Tuple.Create(remoteNode, blockHeaders));
                this.flushWorker.NotifyWork();
            }
            else
            {
                DateTime ignore;
                this.headersRequestsByPeer.TryRemove(remoteNode.RemoteEndPoint, out ignore);
            }
        }

        private void HandleTargetChainChanged(object sender, EventArgs e)
        {
            this.NotifyWork();
        }

        private static ImmutableArray<UInt256> CalculateBlockLocatorHashes(IImmutableList<ChainedHeader> blockHashes)
        {
            var blockLocatorHashes = ImmutableArray.CreateBuilder<UInt256>();

            if (blockHashes.Count > 0)
            {
                var step = 1;
                var start = 0;
                for (var i = blockHashes.Count - 1; i > 0; i -= step, start++)
                {
                    if (start >= 10)
                        step *= 2;

                    blockLocatorHashes.Add(blockHashes[i].Hash);
                }
                blockLocatorHashes.Add(blockHashes[0].Hash);
            }

            return blockLocatorHashes.ToImmutable();
        }
    }
}
