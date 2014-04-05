using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using BitSharp.Data;
using BitSharp.Network;
using BitSharp.Storage;
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

namespace BitSharp.Node
{
    public class BlockRequestWorker : Worker
    {
        private static readonly TimeSpan STALE_REQUEST_TIME = TimeSpan.FromSeconds(60);

        private readonly Logger logger;
        private readonly LocalClient localClient;
        private readonly BlockchainDaemon blockchainDaemon;
        private readonly ChainedBlockCache chainedBlockCache;
        private readonly BlockCache blockCache;

        private readonly ConcurrentDictionary<UInt256, DateTime> allBlockRequests;
        private readonly ConcurrentDictionary<IPEndPoint, ConcurrentDictionary<UInt256, DateTime>> blockRequestsByPeer;

        private SortedList<int, ChainedBlock> missingBlockQueue;
        private int missingBlockQueueIndex;

        private List<ChainedBlock> targetChainQueue;
        private int targetChainQueueIndex;
        private DateTime targetChainQueueTime;

        private readonly TimeSpan[] blockTimes;
        private int blockTimesIndex;
        private readonly ReaderWriterLockSlim blockTimesIndexLock;

        private int targetChainLookAhead;
        private int criticalTargetChainLookAhead;
        private DateTime targetChainLookAheadTime;

        private readonly WorkerMethod flushWorker;
        private readonly ConcurrentQueue<Tuple<RemoteNode, Block>> flushQueue;

        public BlockRequestWorker(Logger logger, WorkerConfig workerConfig, LocalClient localClient, BlockchainDaemon blockchainDaemon, ChainedBlockCache chainedBlockCache, BlockCache blockCache)
            : base("BlockRequestWorker", workerConfig.initialNotify, workerConfig.minIdleTime, workerConfig.maxIdleTime, logger)
        {
            this.logger = logger;
            this.localClient = localClient;
            this.blockchainDaemon = blockchainDaemon;
            this.chainedBlockCache = chainedBlockCache;
            this.blockCache = blockCache;

            this.allBlockRequests = new ConcurrentDictionary<UInt256, DateTime>();
            this.blockRequestsByPeer = new ConcurrentDictionary<IPEndPoint, ConcurrentDictionary<UInt256, DateTime>>();
            this.missingBlockQueue = new SortedList<int, ChainedBlock>();
            this.missingBlockQueueIndex = 0;

            this.localClient.OnBlock += HandleBlock;
            this.blockchainDaemon.OnChainStateChanged += HandleChainStateChanged;
            this.blockchainDaemon.OnChainStateBuilderChanged += HandleChainStateChanged;
            this.blockchainDaemon.OnTargetChainChanged += HandleTargetChainChanged;
            this.blockCache.OnMissing += HandleBlockMissing;

            this.blockTimes = new TimeSpan[10000];
            this.blockTimesIndex = -1;
            this.blockTimesIndexLock = new ReaderWriterLockSlim();

            this.targetChainLookAhead = 1;
            this.criticalTargetChainLookAhead = 1;

            this.flushWorker = new WorkerMethod("BlockRequestWorker.FlushWorker", FlushWorkerMethod, initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: this.logger);
            this.flushQueue = new ConcurrentQueue<Tuple<RemoteNode, Block>>();
        }

        protected override void SubDispose()
        {
            this.localClient.OnBlock -= HandleBlock;
            this.blockchainDaemon.OnChainStateChanged -= HandleChainStateChanged;
            this.blockchainDaemon.OnChainStateBuilderChanged -= HandleChainStateChanged;
            this.blockchainDaemon.OnTargetChainChanged -= HandleTargetChainChanged;
            this.blockCache.OnMissing -= HandleBlockMissing;

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
            // update rates
            new MethodTimer(false).Time("UpdateLookAhead", () =>
                UpdateLookAhead());

            // update list of missing blocks to request
            new MethodTimer(false).Time("UpdateMissingBlockQueue", () =>
                UpdateMissingBlockQueue());

            // update list of blocks on target chain to request
            new MethodTimer(false).Time("UpdateTargetChainQueue", () =>
                UpdateTargetChainQueue());

            // send out request to peers
            //      missing blocks will be requested from every peer
            //      target chain blocks will be requested from each peer in non-overlapping chunks
            new MethodTimer(false).Time("SendBlockRequests", () =>
                SendBlockRequests());
        }

        private void UpdateLookAhead()
        {
            // update periodically
            if (DateTime.UtcNow - this.targetChainLookAheadTime <= TimeSpan.FromSeconds(5))
                return;
            else
                this.targetChainLookAheadTime = DateTime.UtcNow;

            // get average block processing time
            var chainStateBlockProcessingTimeLocal = this.blockchainDaemon.AverageBlockProcessingTime();
            if (chainStateBlockProcessingTimeLocal.Ticks == 0)
                return;

            // get average block request time
            var avgBlockRequestTime = TimeSpan.FromTicks((long)(this.blockTimes.Where(x => x.Ticks > 0).AverageOrDefault(x => x.Ticks) ?? 0));

            // determine target chain look ahead
            var lookAheadTime = avgBlockRequestTime + TimeSpan.FromSeconds(30);
            this.targetChainLookAhead = (int)Math.Max(1, lookAheadTime.Ticks / chainStateBlockProcessingTimeLocal.Ticks);

            // determine critical target chain look ahead
            var criticalLookAheadTime = avgBlockRequestTime + TimeSpan.FromMilliseconds(500);
            this.criticalTargetChainLookAhead = (int)Math.Max(1, criticalLookAheadTime.Ticks / chainStateBlockProcessingTimeLocal.Ticks);

            this.logger.Debug(new string('-', 80));
            this.logger.Debug("Block Processing Time: {0}".Format2(chainStateBlockProcessingTimeLocal));
            this.logger.Debug("Block Processing Rate: {0:#,##0.000}/s".Format2(1 / chainStateBlockProcessingTimeLocal.TotalSeconds));
            this.logger.Debug("Block Request Time: {0}".Format2(avgBlockRequestTime));
            this.logger.Debug("Look Ahead: {0:#,##0}".Format2(this.targetChainLookAhead));
            this.logger.Debug("Critical Look Ahead: {0:#,##0}".Format2(this.criticalTargetChainLookAhead));
            this.logger.Debug("Missing Block Queue Count: {0:#,##0}".Format2(this.missingBlockQueue.Count));
            this.logger.Debug("Block Request Count: {0:#,##0}".Format2(this.allBlockRequests.Count));
            this.logger.Debug(new string('-', 80));
        }

        private void UpdateMissingBlockQueue()
        {
            var currentChainLocal = this.blockchainDaemon.CurrentBuilderChain;
            var targetChainLocal = this.blockchainDaemon.TargetChain;

            // remove any blocks that are no longer missing
            this.missingBlockQueue.RemoveWhere(x => this.blockCache.ContainsKey(x.Value.BlockHash));

            // remove old missing blocks
            this.missingBlockQueue.RemoveWhere(x => x.Value.Height < currentChainLocal.Height);

            // add any blocks that are currently missing
            foreach (var missingBlock in this.blockCache.MissingData)
            {
                ChainedBlock missingBlockChained;
                if (this.chainedBlockCache.TryGetValue(missingBlock, out missingBlockChained))
                {
                    this.missingBlockQueue[missingBlockChained.Height] = missingBlockChained;
                }
            }

            // preemptively add any upcoming blocks on the target chain that are missing
            if (targetChainLocal != null)
            {
                foreach (var upcomingBlock in
                    currentChainLocal.NavigateTowards(targetChainLocal)
                    .Select(x => x.Item2)
                    .Take(this.criticalTargetChainLookAhead)
                    .Where(x =>
                        !this.missingBlockQueue.ContainsKey(x.Height)
                        && !this.blockCache.ContainsKey(x.BlockHash)))
                {
                    this.missingBlockQueue[upcomingBlock.Height] = upcomingBlock;
                }
            }
        }

        private void UpdateTargetChainQueue()
        {
            var currentChainLocal = this.blockchainDaemon.CurrentBuilderChain;
            var targetChainLocal = this.blockchainDaemon.TargetChain;

            // update the target chain queue at most once per second
            if (this.targetChainQueueTime != null && DateTime.UtcNow - targetChainQueueTime < TimeSpan.FromSeconds(1))
                return;
            else
                this.targetChainQueueTime = DateTime.UtcNow;

            // find missing blocks on the target chain to be requested, taking a chunk at a time
            if (targetChainLocal != null &&
                (this.targetChainQueue == null || this.targetChainQueueIndex >= this.targetChainQueue.Count))
            {
                this.targetChainQueue = currentChainLocal.NavigateTowards(targetChainLocal)
                    .Select(x => x.Item2)
                    .Take(this.targetChainLookAhead)
                    .Where(x => !this.blockCache.ContainsKey(x.BlockHash))
                    .ToList();
                this.targetChainQueueIndex = 0;
            }
        }

        private void SendBlockRequests()
        {
            if (this.targetChainQueue == null || this.targetChainQueueIndex >= this.targetChainQueue.Count)
                return;

            var now = DateTime.UtcNow;
            var requestTasks = new List<Task>();
            this.missingBlockQueueIndex = 0;

            // remove any stale requests from the global list of requests
            this.allBlockRequests.RemoveWhere(x => (now - x.Value) > STALE_REQUEST_TIME);

            var peerCount = this.localClient.ConnectedPeers.Count;
            if (peerCount == 0)
                return;

            var requestsPerPeer = Math.Max(1, this.missingBlockQueue.Count + (this.targetChainLookAhead / peerCount * 5));

            // loop through each connected peer
            foreach (var peer in this.localClient.ConnectedPeers)
            {
                // retrieve the peer's currently requested blocks
                var peerBlockRequests = this.blockRequestsByPeer.AddOrUpdate(
                    peer.Key,
                    addKey => new ConcurrentDictionary<UInt256, DateTime>(),
                    (existingKey, existingValue) => existingValue);

                // remove any stale requests from the peer's list of requests
                peerBlockRequests.RemoveWhere(x => (now - x.Value) > STALE_REQUEST_TIME);

                // determine the number of requests that can be sent to the peer
                var requestCount = requestsPerPeer - peerBlockRequests.Count;
                if (requestCount > 0)
                {
                    // iterate through the blocks that should be requested for this peer
                    var invVectors = new List<InventoryVector>();
                    foreach (var requestBlock in GetRequestBlocksForPeer(requestCount, peerBlockRequests))
                    {
                        // track block requests
                        peerBlockRequests[requestBlock] = now;
                        this.allBlockRequests.TryAdd(requestBlock, now);

                        // add block to inv request
                        invVectors.Add(new InventoryVector(InventoryVector.TYPE_MESSAGE_BLOCK, requestBlock));
                    }

                    // send out the request for blocks
                    requestTasks.Add(peer.Value.Sender.SendGetData(invVectors.ToImmutableArray()));
                }
            }

            // notify for another loop of work when out of target chain queue to use, unless there is nothing left missing
            if (this.targetChainQueueIndex >= this.targetChainQueue.Count && this.missingBlockQueue.Count > 0)
                this.ForceWork();
        }

        private IEnumerable<UInt256> GetRequestBlocksForPeer(int count, ConcurrentDictionary<UInt256, DateTime> peerBlockRequests)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            else if (count == 0)
                yield break;

            // keep track of blocks iterated blocks for peer
            var currentCount = 0;

            // iterate through any missing blocks, all peers will request the same missing blocks
            if (this.missingBlockQueue.Count > 0)
            {
                if (this.missingBlockQueueIndex >= this.missingBlockQueue.Count)
                    this.missingBlockQueueIndex = 0;

                var startIndex = this.missingBlockQueueIndex;
                while (true)
                {
                    var missingBlock = this.missingBlockQueue.Values[this.missingBlockQueueIndex];
                    if (!peerBlockRequests.ContainsKey(missingBlock.BlockHash))
                    {
                        yield return missingBlock.BlockHash;

                        currentCount++;
                        if (currentCount >= count)
                            yield break;
                    }

                    this.missingBlockQueueIndex++;
                    if (this.missingBlockQueueIndex >= this.missingBlockQueue.Count)
                        this.missingBlockQueueIndex = 0;

                    if (this.missingBlockQueueIndex == startIndex)
                        break;
                }
            }

            // iterate through the blocks on the target chain, each peer will request a separate chunk of blocks
            for (; this.targetChainQueueIndex < this.targetChainQueue.Count && currentCount < count; this.targetChainQueueIndex++)
            {
                var requestBlock = this.targetChainQueue[this.targetChainQueueIndex].BlockHash;

                if (!peerBlockRequests.ContainsKey(requestBlock)
                    && !this.allBlockRequests.ContainsKey(requestBlock)
                    && !this.blockCache.ContainsKey(requestBlock))
                {
                    yield return requestBlock;
                    currentCount++;
                }
            }
        }

        private void FlushWorkerMethod()
        {
            Tuple<RemoteNode, Block> tuple;
            while (this.flushQueue.TryDequeue(out tuple))
            {
                var remoteNode = tuple.Item1;
                var block = tuple.Item2;

                this.blockCache.TryAdd(block.Hash, block);

                DateTime requestTime;
                if (this.allBlockRequests.TryRemove(block.Hash, out requestTime))
                {
                    this.blockTimesIndexLock.DoWrite(() =>
                    {
                        this.blockTimesIndex = (this.blockTimesIndex + 1) % this.blockTimes.Length;
                        this.blockTimes[this.blockTimesIndex] = DateTime.UtcNow - requestTime;
                    });
                }

                ConcurrentDictionary<UInt256, DateTime> peerBlockRequests;
                if (this.blockRequestsByPeer.TryGetValue(remoteNode.RemoteEndPoint, out peerBlockRequests))
                {
                    peerBlockRequests.TryRemove(block.Hash, out requestTime);
                }

                this.NotifyWork();
            }
        }

        private void HandleBlock(RemoteNode remoteNode, Block block)
        {
            this.flushQueue.Enqueue(Tuple.Create(remoteNode, block));
            this.flushWorker.NotifyWork();
        }

        private void HandleChainStateChanged(object sender, EventArgs e)
        {
            this.NotifyWork();
        }

        private void HandleTargetChainChanged(object sender, EventArgs e)
        {
            this.NotifyWork();
        }

        private void HandleBlockMissing(UInt256 blockHash)
        {
            this.NotifyWork();
        }

        private sealed class HeightComparer : IComparer<ChainedBlock>
        {
            public int Compare(ChainedBlock x, ChainedBlock y)
            {
                return x.Height - y.Height;
            }
        }
    }
}
