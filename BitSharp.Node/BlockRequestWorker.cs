using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using BitSharp.Data;
using BitSharp.Network;
using BitSharp.Storage;
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
        private const int REQUESTS_PER_PEER = 100;
        private const int STALE_REQUEST_SECONDS = 60;

        private const int TARGET_CHAIN_CHUNK_COUNT = 1000;
        private const int UPCOMING_AS_MISSING_CHUNK_COUNT = 100;

        private readonly LocalClient localClient;
        private readonly BlockchainDaemon blockchainDaemon;
        private readonly ICacheContext cacheContext;

        private readonly ConcurrentDictionary<UInt256, DateTime> allBlockRequests;
        private readonly ConcurrentDictionary<IPEndPoint, ConcurrentDictionary<UInt256, DateTime>> blockRequestsByPeer;

        private SortedSet<ChainedBlock> missingBlockQueue;

        private List<ChainedBlock> targetChainQueue;
        private int targetChainQueueIndex;
        private DateTime targetChainQueueTime;

        private readonly TimeSpan[] blockTimes;
        private int blockTimesIndex;
        private readonly ReaderWriterLockSlim blockTimesIndexLock;

        private int targetChainLookAhead;

        private readonly WorkerMethod flushWorker;
        private readonly ConcurrentQueue<Tuple<RemoteNode, Block>> flushQueue;

        public BlockRequestWorker(LocalClient localClient, bool initialNotify, TimeSpan minIdleTime, TimeSpan maxIdleTime)
            : base("BlockRequestWorker", initialNotify, minIdleTime, maxIdleTime)
        {
            this.localClient = localClient;
            this.blockchainDaemon = localClient.BlockchainDaemon;
            this.cacheContext = localClient.CacheContext;

            this.allBlockRequests = new ConcurrentDictionary<UInt256, DateTime>();
            this.blockRequestsByPeer = new ConcurrentDictionary<IPEndPoint, ConcurrentDictionary<UInt256, DateTime>>();
            this.missingBlockQueue = new SortedSet<ChainedBlock>(new HeightComparer());

            this.localClient.OnBlock += HandleBlock;
            this.blockchainDaemon.OnChainStateChanged += HandleChainStateChanged;
            this.blockchainDaemon.OnChainStateBuilderChanged += HandleChainStateChanged;

            this.blockTimes = new TimeSpan[1000];
            this.blockTimesIndex = -1;
            this.blockTimesIndexLock = new ReaderWriterLockSlim();

            this.targetChainLookAhead = 1;

            this.flushWorker = new WorkerMethod("BlockRequestWorker.FlushWorker", FlushWorkerMethod, initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue);
            this.flushQueue = new ConcurrentQueue<Tuple<RemoteNode, Block>>();
        }

        protected override void SubDispose()
        {
            this.localClient.OnBlock -= HandleBlock;
            this.blockchainDaemon.OnChainStateChanged -= HandleChainStateChanged;
            this.blockchainDaemon.OnChainStateBuilderChanged -= HandleChainStateChanged;

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

        private void UpdateMissingBlockQueue()
        {
            var currentChainLocal = this.blockchainDaemon.CurrentBuilderChain;
            var targetChainLocal = this.blockchainDaemon.TargetChain;

            // remove any blocks that are no longer missing
            this.missingBlockQueue.RemoveWhere(x => this.cacheContext.BlockView.ContainsKey(x.BlockHash));

            // add any blocks that are currently missing
            foreach (var missingBlock in this.cacheContext.BlockView.MissingData)
            {
                ChainedBlock missingBlockChained;
                if (this.cacheContext.ChainedBlockCache.TryGetValue(missingBlock, out missingBlockChained))
                {
                    if (!this.missingBlockQueue.Contains(missingBlockChained))
                        this.missingBlockQueue.Add(missingBlockChained);
                }
            }

            // preemptively add any upcoming blocks on the target chain that are missing
            if (targetChainLocal != null)
            {
                foreach (var upcomingBlock in
                    currentChainLocal.NavigateTowards(targetChainLocal)
                    .Select(x => x.Item2)
                    .Take(Math.Min(UPCOMING_AS_MISSING_CHUNK_COUNT, this.targetChainLookAhead))
                    .Where(x =>
                        !this.missingBlockQueue.Contains(x)
                        && !this.blockchainDaemon.CacheContext.BlockView.ContainsKey(x.BlockHash)))
                {
                    this.missingBlockQueue.Add(upcomingBlock);
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
                    .Where(x => !this.blockchainDaemon.CacheContext.BlockView.ContainsKey(x.BlockHash))
                    .Take(TARGET_CHAIN_CHUNK_COUNT)
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

            // remove any stale requests from the global list of requests
            this.allBlockRequests.RemoveWhere(x => (now - x.Value) > TimeSpan.FromSeconds(STALE_REQUEST_SECONDS));

            // loop through each connected peer
            foreach (var peer in this.localClient.ConnectedPeers)
            {
                // retrieve the peer's currently requested blocks
                var peerBlockRequests = this.blockRequestsByPeer.AddOrUpdate(
                    peer.Key,
                    addKey => new ConcurrentDictionary<UInt256, DateTime>(),
                    (existingKey, existingValue) => existingValue);

                // remove any stale requests from the peer's list of requests
                peerBlockRequests.RemoveWhere(x => (now - x.Value) > TimeSpan.FromSeconds(STALE_REQUEST_SECONDS));

                // determine the number of requests that can be sent to the peer
                var requestCount = REQUESTS_PER_PEER - peerBlockRequests.Count;
                if (requestCount > 0)
                {
                    // iterate through the blocks that should be requested for this peer
                    var invVectors = new List<InventoryVector>();
                    foreach (var requestBlock in GetRequestBlocksForPeer(requestCount, peerBlockRequests))
                    {
                        // track block requests
                        peerBlockRequests[requestBlock] = now;
                        this.allBlockRequests[requestBlock] = now;

                        // add block to inv request
                        invVectors.Add(new InventoryVector(InventoryVector.TYPE_MESSAGE_BLOCK, requestBlock));
                    }

                    // send out the request for blocks
                    requestTasks.Add(peer.Value.Sender.SendGetData(invVectors.ToImmutableArray()));
                }
            }

            // notify for another loop of work when out of target chain queue to use, unless there is nothing left missing
            if (this.targetChainQueueIndex >= this.targetChainQueue.Count && this.missingBlockQueue.Count > 0)
                this.NotifyWork();
        }

        private IEnumerable<UInt256> GetRequestBlocksForPeer(int count, ConcurrentDictionary<UInt256, DateTime> peerBlockRequests)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            else if (count == 0)
                yield break;

            // iterate through any missing blocks, all peers will request the same missing blocks
            var currentCount = 0;
            foreach (var missingBlock in this.missingBlockQueue)
            {
                if (!peerBlockRequests.ContainsKey(missingBlock.BlockHash))
                {
                    yield return missingBlock.BlockHash;

                    currentCount++;
                    if (currentCount >= count)
                        yield break;
                }
            }

            // iterate through the blocks on the target chain, each peer will request a separate chunk of blocks
            for (; this.targetChainQueueIndex < this.targetChainQueue.Count && currentCount < count; this.targetChainQueueIndex++)
            {
                var requestBlock = this.targetChainQueue[this.targetChainQueueIndex].BlockHash;

                if (!peerBlockRequests.ContainsKey(requestBlock)
                    && !this.allBlockRequests.ContainsKey(requestBlock)
                    && !this.cacheContext.BlockView.ContainsKey(requestBlock))
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

                this.cacheContext.BlockView.TryAdd(block.Hash, block);

                DateTime requestTime;
                if (this.allBlockRequests.TryRemove(block.Hash, out requestTime))
                {
                    var blockTimesIndexLocal = this.blockTimesIndexLock.DoWrite(() =>
                    {
                        this.blockTimesIndex = (this.blockTimesIndex + 1) % this.blockTimes.Length;
                        return this.blockTimesIndex;
                    });
                    this.blockTimes[blockTimesIndexLocal] = DateTime.UtcNow - requestTime;

                    var lookAheadTime = TimeSpan.FromTicks((long)this.blockTimes.Average(x => x.Ticks))
                        + TimeSpan.FromSeconds(5);

                    this.targetChainLookAhead = (int)Math.Max(1, lookAheadTime.Ticks / this.blockchainDaemon.ChainStateBlockProcessingTime.Ticks);
                }

                ConcurrentDictionary<UInt256, DateTime> peerBlockRequests;
                if (this.blockRequestsByPeer.TryGetValue(remoteNode.RemoteEndPoint, out peerBlockRequests))
                {
                    peerBlockRequests.TryRemove(block.Hash, out requestTime);
                }

                this.NotifyWork();
            }
            
            //Debug.WriteLine("Look-ahead: {0}".Format2(this.lookAhead));
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

        private sealed class HeightComparer : IComparer<ChainedBlock>
        {
            public int Compare(ChainedBlock x, ChainedBlock y)
            {
                return x.Height - y.Height;
            }
        }
    }
}
