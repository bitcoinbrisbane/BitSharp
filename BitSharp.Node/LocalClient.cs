using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Node.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using Ninject;
using Ninject.Parameters;
using NLog;
using BitSharp.Node.Network;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core;
using BitSharp.Node.Workers;
using BitSharp.Node.Domain;
using BitSharp.Node.Storage;

namespace BitSharp.Node
{
    public class LocalClient : IDisposable
    {
        public event Action<RemoteNode, Block> OnBlock;
        public event Action<RemoteNode, IImmutableList<BlockHeader>> OnBlockHeaders;

        private static readonly int SERVER_BACKLOG = 10;
        private static readonly int CONNECTED_MAX = 25;
        private static readonly int PENDING_MAX = 2 * CONNECTED_MAX;
        private static readonly int HANDSHAKE_TIMEOUT_MS = 15000;

        private static readonly Random random = new Random();

        private readonly Logger logger;
        private readonly CancellationTokenSource shutdownToken;

        private readonly RulesEnum type;
        private readonly IKernel kernel;
        private readonly IBlockchainRules rules;
        private readonly CoreDaemon blockchainDaemon;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly ChainedHeaderCache chainedHeaderCache;
        private readonly TransactionCache transactionCache;
        private readonly BlockCache blockCache;
        private readonly NetworkPeerCache networkPeerCache;

        private readonly WorkerMethod connectWorker;
        private readonly HeadersRequestWorker headersRequestWorker;
        private readonly BlockRequestWorker blockRequestWorker;
        private readonly WorkerMethod statsWorker;

        private Stopwatch messageStopwatch = new Stopwatch();
        private int messageCount;

        private int incomingCount;
        private ConcurrentSet<CandidatePeer> unconnectedPeers = new ConcurrentSet<CandidatePeer>();
        private readonly ReaderWriterLockSlim unconnectedPeersLock = new ReaderWriterLockSlim();
        private ConcurrentSet<IPEndPoint> badPeers = new ConcurrentSet<IPEndPoint>();
        private ConcurrentDictionary<IPEndPoint, RemoteNode> pendingPeers = new ConcurrentDictionary<IPEndPoint, RemoteNode>();
        private ConcurrentDictionary<IPEndPoint, RemoteNode> connectedPeers = new ConcurrentDictionary<IPEndPoint, RemoteNode>();

        private readonly ConcurrentDictionary<UInt256, DateTime> requestedTransactions = new ConcurrentDictionary<UInt256, DateTime>();

        private Socket listenSocket;

        public LocalClient(Logger logger, RulesEnum type, IKernel kernel, IBlockchainRules rules, CoreDaemon blockchainDaemon, BlockHeaderCache blockHeaderCache, ChainedHeaderCache chainedHeaderCache, TransactionCache transactionCache, BlockCache blockCache, NetworkPeerCache networkPeerCache)
        {
            this.shutdownToken = new CancellationTokenSource();

            this.logger = logger;
            this.type = type;
            this.kernel = kernel;
            this.rules = rules;
            this.blockchainDaemon = blockchainDaemon;
            this.blockHeaderCache = blockHeaderCache;
            this.chainedHeaderCache = chainedHeaderCache;
            this.transactionCache = transactionCache;
            this.blockCache = blockCache;
            this.networkPeerCache = networkPeerCache;

            this.connectWorker = new WorkerMethod("LocalClient.ConnectWorker", ConnectWorker, true, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), this.logger);
            this.headersRequestWorker = kernel.Get<HeadersRequestWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromMilliseconds(50), maxIdleTime: TimeSpan.FromSeconds(30))),
                new ConstructorArgument("localClient", this));
            this.blockRequestWorker = kernel.Get<BlockRequestWorker>(
                new ConstructorArgument("workerConfig", new WorkerConfig(initialNotify: true, minIdleTime: TimeSpan.FromMilliseconds(50), maxIdleTime: TimeSpan.FromSeconds(30))),
                new ConstructorArgument("localClient", this));
            this.statsWorker = new WorkerMethod("LocalClient.StatsWorker", StatsWorker, true, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), this.logger);

            switch (this.Type)
            {
                case RulesEnum.MainNet:
                    Messaging.Port = 8333;
                    Messaging.Magic = Messaging.MAGIC_MAIN;
                    break;

                case RulesEnum.TestNet3:
                    Messaging.Port = 18333;
                    Messaging.Magic = Messaging.MAGIC_TESTNET3;
                    break;

                case RulesEnum.ComparisonToolTestNet:
                    Messaging.Port = 18444;
                    Messaging.Magic = Messaging.MAGIC_COMPARISON_TOOL;
                    break;
            }
        }

        public RulesEnum Type { get { return this.type; } }

        internal ConcurrentDictionary<IPEndPoint, RemoteNode> ConnectedPeers { get { return this.connectedPeers; } }

        public void Start()
        {
            Startup();

            this.connectWorker.Start();

            if (this.Type != RulesEnum.ComparisonToolTestNet)
            {
                this.headersRequestWorker.Start();
            }
            this.blockRequestWorker.Start();

            this.messageStopwatch.Start();
            this.messageCount = 0;
            this.statsWorker.Start();
        }

        public void Dispose()
        {
            this.shutdownToken.Cancel();

            Shutdown();

            new IDisposable[]
            {
                this.headersRequestWorker,
                this.blockRequestWorker,
                this.connectWorker,
                this.statsWorker,
                this.shutdownToken
            }.DisposeList();
        }

        public float GetBlockDownloadRate(TimeSpan perUnitTime)
        {
            return this.blockRequestWorker.GetBlockDownloadRate(perUnitTime);
        }

        public float GetDuplicateBlockDownloadRate(TimeSpan perUnitTime)
        {
            return this.blockRequestWorker.GetDuplicateBlockDownloadRate(perUnitTime);
        }

        private void ConnectWorker()
        {
            if (this.Type == RulesEnum.ComparisonToolTestNet)
                return;

            // get peer counts
            var connectedCount = this.connectedPeers.Count;
            var pendingCount = this.pendingPeers.Count;
            var unconnectedCount = this.unconnectedPeers.Count;
            var maxConnections = Math.Max(CONNECTED_MAX + 20, PENDING_MAX);

            // if there aren't enough peers connected and there is a pending connection slot available, make another connection
            if (connectedCount < CONNECTED_MAX
                 && pendingCount < PENDING_MAX
                 && (connectedCount + pendingCount) < maxConnections
                 && unconnectedCount > 0)
            {
                // get number of connections to attempt
                var connectCount = Math.Min(unconnectedCount, maxConnections - (connectedCount + pendingCount));

                var unconnectedPeersSorted = this.unconnectedPeers.OrderBy(x => -x.Time.Ticks).ToArray();
                var unconnectedPeerIndex = 0;

                var connectTasks = new List<Task>();
                for (var i = 0; i < connectCount && unconnectedPeerIndex < unconnectedPeersSorted.Length; i++)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // get a random peer to connect to
                    var candidatePeer = unconnectedPeersSorted[unconnectedPeerIndex];
                    unconnectedPeerIndex++;

                    connectTasks.Add(ConnectToPeer(candidatePeer.IPEndPoint));
                }

                // wait for pending connection attempts to complete
                //Task.WaitAll(connectTasks.ToArray(), this.shutdownToken.Token);
            }

            // check if there are too many peers connected
            var overConnected = this.connectedPeers.Count - CONNECTED_MAX;
            if (overConnected > 0)
            {
                foreach (var remoteEndpoint in this.connectedPeers.Keys.Take(overConnected))
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    this.logger.Debug("Too many peers connected ({0}), disconnecting {1}".Format2(overConnected, remoteEndpoint));
                    DisconnectPeer(remoteEndpoint, null);
                }
            }
        }

        private void StatsWorker()
        {
            this.logger.Info(string.Format("UNCONNECTED: {0,3}, PENDING: {1,3}, CONNECTED: {2,3}, BAD: {3,3}, INCOMING: {4,3}, MESSAGES/SEC: {5,6}", this.unconnectedPeers.Count, this.pendingPeers.Count, this.connectedPeers.Count, this.badPeers.Count, this.incomingCount, ((float)this.messageCount / ((float)this.messageStopwatch.ElapsedMilliseconds / 1000)).ToString("0")));

            this.messageStopwatch.Restart();
            this.messageCount = 0;
        }

        private void Startup()
        {
            this.logger.Info("LocalClients starting up");
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // start listening for incoming peers
            StartListening();

            // add seed peers
            AddSeedPeers();

            // add known peers
            AddKnownPeers();

            stopwatch.Stop();
            this.logger.Info("LocalClients finished starting up: {0} ms".Format2(stopwatch.ElapsedMilliseconds));
        }

        private void Shutdown()
        {
            this.logger.Info("LocalClient shutting down");

            try
            {
                foreach (var remoteNode in this.connectedPeers.Values)
                {
                    try
                    {
                        remoteNode.Disconnect();
                    }
                    catch (Exception) { } // swallow any exceptions at the peer disconnect level to try and process everyone
                }
            }
            catch (Exception) { } // swallow any looping exceptions

            this.logger.Info("LocalClient shutdown finished");
        }

        private async void StartListening()
        {
            try
            {
                switch (this.Type)
                {
                    case RulesEnum.MainNet:
                    case RulesEnum.TestNet3:
                        var externalIPAddress = Messaging.GetExternalIPAddress();
                        var localhost = Dns.GetHostEntry(Dns.GetHostName());

                        this.listenSocket = new Socket(externalIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        this.listenSocket.Bind(new IPEndPoint(localhost.AddressList.Where(x => x.AddressFamily == externalIPAddress.AddressFamily).First(), Messaging.Port));
                        break;

                    case RulesEnum.ComparisonToolTestNet:
                        this.listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        this.listenSocket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), Messaging.Port));
                        break;
                }
                this.listenSocket.Listen(SERVER_BACKLOG);
            }
            catch (Exception e)
            {
                this.logger.ErrorException("Failed to start listener socket.", e);
                if (this.listenSocket != null)
                    this.listenSocket.Dispose();
                return;
            }

            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    try
                    {
                        var newSocket = await Task.Factory.FromAsync<Socket>(this.listenSocket.BeginAccept(null, null), this.listenSocket.EndAccept);

                        Task.Run(async () =>
                        {
                            var remoteNode = new RemoteNode(newSocket);
                            try
                            {
                                if (await ConnectAndHandshake(remoteNode, isIncoming: true))
                                {
                                    Interlocked.Increment(ref this.incomingCount);
                                }
                                else
                                {
                                    DisconnectPeer(remoteNode.RemoteEndPoint, null);
                                }
                            }
                            catch (Exception e)
                            {
                                if (remoteNode.RemoteEndPoint != null)
                                    DisconnectPeer(remoteNode.RemoteEndPoint, e);
                            }
                        }).Forget();
                    }
                    catch (Exception e)
                    {
                        this.logger.WarnException("Failed incoming connection.", e);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                this.listenSocket.Dispose();
            }
        }

        private async Task PeerStartup(RemoteNode remoteNode)
        {
            await remoteNode.Sender.RequestKnownAddressesAsync();
        }

        private void AddSeedPeers()
        {
            Action<string> addSeed =
                hostNameOrAddress =>
                {
                    try
                    {
                        var ipAddress = Dns.GetHostEntry(hostNameOrAddress).AddressList.First();
                        this.unconnectedPeers.TryAdd(
                            new CandidatePeer
                            {
                                IPEndPoint = new IPEndPoint(ipAddress, Messaging.Port),
                                Time = DateTime.UtcNow
                            });
                    }
                    catch (SocketException e)
                    {
                        this.logger.WarnException("Failed to add seed peer {0}".Format2(hostNameOrAddress), e);
                    }
                };

            switch (this.Type)
            {
                case RulesEnum.MainNet:
                    addSeed("archivum.info");
                    addSeed("62.75.216.13");
                    addSeed("69.64.34.118");
                    addSeed("79.160.221.140");
                    addSeed("netzbasis.de");
                    addSeed("fallback.bitcoin.zhoutong.com");
                    addSeed("bauhaus.csail.mit.edu");
                    addSeed("jun.dashjr.org");
                    addSeed("cheaperinbitcoins.com");
                    addSeed("django.webflows.fr");
                    addSeed("204.9.55.71");
                    addSeed("btcnode.novit.ro");
                    //No such host is known: addSeed("porgressbar.sk");
                    addSeed("faucet.bitcoin.st");
                    addSeed("bitcoin.securepayment.cc");
                    addSeed("x.jine.se");
                    //addSeed("www.dcscdn.com");
                    //addSeed("ns2.dcscdn.com");
                    //No such host is known: addSeed("coin.soul-dev.com");
                    addSeed("messier.bzfx.net");
                    break;

                case RulesEnum.TestNet3:
                    addSeed("testnet-seed.bitcoin.petertodd.org");
                    addSeed("testnet-seed.bluematt.me");
                    break;
            }
        }

        private void AddKnownPeers()
        {
            var count = 0;
            foreach (var knownAddress in this.networkPeerCache.Values)
            {
                this.unconnectedPeers.TryAdd(
                    new CandidatePeer
                    {
                        IPEndPoint = knownAddress.NetworkAddress.ToIPEndPoint(),
                        Time = knownAddress.Time.UnixTimeToDateTime()
                    });
                count++;
            }

            this.logger.Info("LocalClients loaded {0} known peers from database".Format2(count));
        }

        private async Task<RemoteNode> ConnectToPeer(IPEndPoint remoteEndPoint)
        {
            try
            {
                var remoteNode = new RemoteNode(remoteEndPoint, this.logger);

                this.unconnectedPeers.TryRemove(remoteEndPoint.ToCandidatePeerKey());
                this.pendingPeers.TryAdd(remoteNode.RemoteEndPoint, remoteNode);

                var success = await ConnectAndHandshake(remoteNode, isIncoming: false);
                if (success)
                {
                    await PeerStartup(remoteNode);

                    return remoteNode;
                }
                else
                {
                    //this.knownAddressCache[remoteEndPoint.ToNetworkAddressKey()] =
                    //    new NetworkAddressWithTime(0, remoteEndPoint.ToNetworkAddress(0));
                    DisconnectPeer(remoteEndPoint, null);
                    return null;
                }
            }
            catch (Exception e)
            {
                this.logger.DebugException("Could not connect to {0}".Format2(remoteEndPoint), e);
                //this.knownAddressCache[remoteEndPoint.ToNetworkAddressKey()] =
                //    new NetworkAddressWithTime(0, remoteEndPoint.ToNetworkAddress(0));
                DisconnectPeer(remoteEndPoint, e);
                return null;
            }
        }

        private void WireNode(RemoteNode remoteNode)
        {
            remoteNode.Receiver.OnMessage += OnMessage;
            remoteNode.Receiver.OnInventoryVectors += OnInventoryVectors;
            remoteNode.Receiver.OnBlock += HandleBlock;
            remoteNode.Receiver.OnBlockHeaders += HandleBlockHeaders;
            remoteNode.Receiver.OnTransaction += OnTransaction;
            remoteNode.Receiver.OnReceivedAddresses += OnReceivedAddresses;
            remoteNode.OnGetBlocks += OnGetBlocks;
            remoteNode.OnGetHeaders += OnGetHeaders;
            remoteNode.OnGetData += OnGetData;
            remoteNode.OnPing += OnPing;
            remoteNode.OnDisconnect += OnDisconnect;
        }

        private void UnwireNode(RemoteNode remoteNode)
        {
            remoteNode.Receiver.OnMessage -= OnMessage;
            remoteNode.Receiver.OnInventoryVectors -= OnInventoryVectors;
            remoteNode.Receiver.OnBlock -= HandleBlock;
            remoteNode.Receiver.OnBlockHeaders -= HandleBlockHeaders;
            remoteNode.Receiver.OnTransaction -= OnTransaction;
            remoteNode.Receiver.OnReceivedAddresses -= OnReceivedAddresses;
            remoteNode.OnGetBlocks -= OnGetBlocks;
            remoteNode.OnGetHeaders -= OnGetHeaders;
            remoteNode.OnGetData -= OnGetData;
            remoteNode.OnPing -= OnPing;
            remoteNode.OnDisconnect -= OnDisconnect;
        }

        private void OnMessage(Message message)
        {
            Interlocked.Increment(ref this.messageCount);
        }

        private void OnInventoryVectors(ImmutableArray<InventoryVector> invVectors)
        {
            var connectedPeersLocal = this.connectedPeers.Values.SafeToList();
            if (connectedPeersLocal.Count == 0)
                return;

            if (this.Type == RulesEnum.ComparisonToolTestNet)
            {
                var responseInvVectors = new List<InventoryVector>();

                foreach (var invVector in invVectors)
                {
                    if (invVector.Type == InventoryVector.TYPE_MESSAGE_BLOCK
                        && !this.blockCache.ContainsKey(invVector.Hash))
                    {
                        responseInvVectors.Add(invVector);
                    }
                }

                connectedPeersLocal.Single().Sender.SendGetData(responseInvVectors.ToImmutableArray()).Forget();
            }
        }

        private Task RequestTransaction(RemoteNode remoteNode, UInt256 txHash)
        {
            //TODO
            //if (this.TransactionCache.ContainsKey(txHash))
            //    return null;

            var now = DateTime.UtcNow;
            var newRequestTime = now;

            // check if transaction has already been requested
            if (this.requestedTransactions.TryAdd(txHash, now))
            {
                var invVectors = ImmutableArray.Create<InventoryVector>(new InventoryVector(InventoryVector.TYPE_MESSAGE_TRANSACTION, txHash));
                return remoteNode.Sender.SendGetData(invVectors);
            }

            return null;
        }

        private void HandleBlock(RemoteNode remoteNode, Block block)
        {
            var handler = this.OnBlock;
            if (handler != null)
                handler(remoteNode, block);
        }

        private void HandleBlockHeaders(RemoteNode remoteNode, IImmutableList<BlockHeader> blockHeaders)
        {
            var handler = this.OnBlockHeaders;
            if (handler != null)
                handler(remoteNode, blockHeaders);
        }

        private void OnTransaction(Transaction transaction)
        {
            if (this.logger.IsTraceEnabled)
                this.logger.Trace("Received block header {0}".Format2(transaction.Hash));

            DateTime ignore;
            this.requestedTransactions.TryRemove(transaction.Hash, out ignore);
            this.transactionCache.TryAdd(transaction.Hash, transaction);
        }

        private void OnReceivedAddresses(ImmutableArray<NetworkAddressWithTime> addresses)
        {
            var ipEndpoints = new List<IPEndPoint>(addresses.Count);
            foreach (var address in addresses)
            {
                var ipEndpoint = address.NetworkAddress.ToIPEndPoint();
                ipEndpoints.Add(ipEndpoint);
            }

            this.unconnectedPeers.UnionWith(ipEndpoints.Select(x => x.ToCandidatePeerKey()));
            this.unconnectedPeers.ExceptWith(this.badPeers.Select(x => x.ToCandidatePeerKey()));
            this.unconnectedPeers.ExceptWith(this.connectedPeers.Keys.Select(x => x.ToCandidatePeerKey()));
            this.unconnectedPeers.ExceptWith(this.pendingPeers.Keys.Select(x => x.ToCandidatePeerKey()));

            // queue up addresses to be flushed to the database
            foreach (var address in addresses)
            {
                //this.knownAddressCache.TryAdd(address.GetKey(), address);
            }
        }

        private void OnGetBlocks(RemoteNode remoteNode, GetBlocksPayload payload)
        {
            var targetChainLocal = this.blockchainDaemon.TargetChain;
            if (targetChainLocal == null)
                return;

            ChainedHeader matchingChainedHeader = null;
            foreach (var blockHash in payload.BlockLocatorHashes)
            {
                ChainedHeader chainedHeader;
                if (this.chainedHeaderCache.TryGetValue(blockHash, out chainedHeader))
                {
                    if (chainedHeader.Height < targetChainLocal.Blocks.Count
                        && chainedHeader.Hash == targetChainLocal.Blocks[chainedHeader.Height].Hash)
                    {
                        matchingChainedHeader = chainedHeader;
                        break;
                    }
                }
            }

            if (matchingChainedHeader == null)
            {
                matchingChainedHeader = this.rules.GenesisChainedHeader;
            }

            var count = 0;
            var limit = 500;
            var invVectors = new InventoryVector[limit];
            for (var i = matchingChainedHeader.Height; i < targetChainLocal.Blocks.Count && count <= limit; i++, count++)
            {
                var chainedHeader = targetChainLocal.Blocks[i];
                invVectors[count] = new InventoryVector(InventoryVector.TYPE_MESSAGE_BLOCK, chainedHeader.Hash);

                if (chainedHeader.Hash == payload.HashStop)
                    break;
            }
            Array.Resize(ref invVectors, count);

            remoteNode.Sender.SendInventory(invVectors.ToImmutableArray()).Forget();
        }

        private void OnGetHeaders(RemoteNode remoteNode, GetBlocksPayload payload)
        {
            if (this.Type == RulesEnum.ComparisonToolTestNet)
            {
                this.blockchainDaemon.ForceWorkAndWait();
            }

            var targetChainLocal = this.blockchainDaemon.TargetChain;
            if (targetChainLocal == null)
                return;

            ChainedHeader matchingChainedHeader = null;
            foreach (var blockHash in payload.BlockLocatorHashes)
            {
                ChainedHeader chainedHeader;
                if (this.chainedHeaderCache.TryGetValue(blockHash, out chainedHeader))
                {
                    if (chainedHeader.Height < targetChainLocal.Blocks.Count
                        && chainedHeader.Hash == targetChainLocal.Blocks[chainedHeader.Height].Hash)
                    {
                        matchingChainedHeader = chainedHeader;
                        break;
                    }
                }
            }

            if (matchingChainedHeader == null)
            {
                matchingChainedHeader = this.rules.GenesisChainedHeader;
            }

            var count = 0;
            var limit = 500;
            var blockHeaders = new BlockHeader[limit];
            for (var i = matchingChainedHeader.Height; i < targetChainLocal.Blocks.Count && count <= limit; i++, count++)
            {
                var chainedHeader = targetChainLocal.Blocks[i];

                BlockHeader blockHeader;
                if (this.blockHeaderCache.TryGetValue(targetChainLocal.Blocks[i].Hash, out blockHeader))
                {
                    blockHeaders[count] = blockHeader;
                }
                else
                {
                    break;
                }

                if (chainedHeader.Hash == payload.HashStop)
                    break;
            }
            Array.Resize(ref blockHeaders, count);

            remoteNode.Sender.SendHeaders(blockHeaders.ToImmutableArray()).Forget();
        }

        private void OnGetData(RemoteNode remoteNode, InventoryPayload payload)
        {
            foreach (var invVector in payload.InventoryVectors)
            {
                switch (invVector.Type)
                {
                    case InventoryVector.TYPE_MESSAGE_BLOCK:
                        Block block;
                        if (this.blockCache.TryGetValue(invVector.Hash, out block))
                        {
                            //remoteNode.Sender.SendBlock(block).Forget();
                        }
                        break;

                    case InventoryVector.TYPE_MESSAGE_TRANSACTION:
                        Transaction transaction;
                        if (this.transactionCache.TryGetValue(invVector.Hash, out transaction))
                        {
                            remoteNode.Sender.SendTransaction(transaction).Forget();
                        }
                        break;
                }
            }
        }

        private void OnPing(RemoteNode remoteNode, ImmutableArray<byte> payload)
        {
            remoteNode.Sender.SendMessageAsync(Messaging.ConstructMessage("pong", payload.ToArray())).Wait();
        }

        private void OnDisconnect(RemoteNode remoteNode)
        {
            DisconnectPeer(remoteNode.RemoteEndPoint, null);
        }

        private async Task<bool> ConnectAndHandshake(RemoteNode remoteNode, bool isIncoming)
        {
            // wire node
            WireNode(remoteNode);

            // connect
            await remoteNode.ConnectAsync();

            if (remoteNode.IsConnected)
            {
                //TODO
                RemoteNode ignore;
                this.pendingPeers.TryRemove(remoteNode.RemoteEndPoint, out ignore);
                this.connectedPeers.TryAdd(remoteNode.RemoteEndPoint, remoteNode);

                // setup task to wait for verack
                var verAckTask = remoteNode.Receiver.WaitForMessage(x => x.Command == "verack", HANDSHAKE_TIMEOUT_MS);

                // setup task to wait for version
                var versionTask = remoteNode.Receiver.WaitForMessage(x => x.Command == "version", HANDSHAKE_TIMEOUT_MS);

                // start listening for messages after tasks have been setup
                remoteNode.Receiver.Listen();

                // send our local version
                var nodeId = random.NextUInt64(); //TODO should be generated and verified on version message

                var currentHeight = this.blockchainDaemon.CurrentChain.Height;
                await remoteNode.Sender.SendVersion(Messaging.GetExternalIPEndPoint(), remoteNode.RemoteEndPoint, nodeId, (UInt32)currentHeight);

                // wait for our local version to be acknowledged by the remote peer
                // wait for remote peer to send their version
                await Task.WhenAll(verAckTask, versionTask);

                //TODO shouldn't have to decode again
                var versionMessage = versionTask.Result;
                var versionPayload = NodeEncoder.DecodeVersionPayload(versionMessage.Payload.ToArray(), versionMessage.Payload.Count);

                var remoteAddressWithTime = new NetworkAddressWithTime
                (
                    Time: DateTime.UtcNow.ToUnixTime(),
                    NetworkAddress: new NetworkAddress
                    (
                        Services: versionPayload.LocalAddress.Services,
                        IPv6Address: versionPayload.LocalAddress.IPv6Address,
                        Port: versionPayload.LocalAddress.Port
                    )
                );

                if (!isIncoming)
                    this.networkPeerCache[remoteAddressWithTime.GetKey()] = remoteAddressWithTime;

                // acknowledge their version
                await remoteNode.Sender.SendVersionAcknowledge();

                this.headersRequestWorker.NotifyWork();
                this.blockRequestWorker.NotifyWork();
                this.statsWorker.NotifyWork();

                return true;
            }
            else
            {
                return false;
            }
        }

        private void DisconnectPeer(IPEndPoint remoteEndpoint, Exception e)
        {
            this.badPeers.Add(remoteEndpoint); //TODO
            this.unconnectedPeers.TryRemove(remoteEndpoint.ToCandidatePeerKey());

            RemoteNode pendingPeer;
            this.pendingPeers.TryRemove(remoteEndpoint, out pendingPeer);

            RemoteNode connectedPeer;
            this.connectedPeers.TryRemove(remoteEndpoint, out connectedPeer);

            this.logger.DebugException("Remote peer failed: {0}".Format2(remoteEndpoint), e);

            if (pendingPeer != null)
            {
                UnwireNode(pendingPeer);
                pendingPeer.Disconnect();
            }

            if (connectedPeer != null)
            {
                UnwireNode(connectedPeer);
                connectedPeer.Disconnect();
            }
        }
    }

    internal sealed class CandidatePeer
    {
        public IPEndPoint IPEndPoint { get; set; }
        public DateTime Time { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is CandidatePeer))
                return false;

            var other = (CandidatePeer)obj;
            return other.IPEndPoint.Equals(this.IPEndPoint);
        }

        public override int GetHashCode()
        {
            return this.IPEndPoint.GetHashCode();
        }
    }

    internal sealed class CandidatePeerComparer : IComparer<CandidatePeer>
    {
        public int Compare(CandidatePeer x, CandidatePeer y)
        {
            if (y.Time.Ticks < x.Time.Ticks)
                return -1;
            else if (y.Time.Ticks > x.Time.Ticks)
                return +1;
            else
                return 0;
        }
    }

    namespace ExtensionMethods
    {
        internal static class LocalClientExtensionMethods
        {
            public static NetworkAddressKey GetKey(this NetworkAddressWithTime knownAddress)
            {
                return new NetworkAddressKey(knownAddress.NetworkAddress.IPv6Address, knownAddress.NetworkAddress.Port);
            }

            public static NetworkAddressKey ToNetworkAddressKey(this IPEndPoint ipEndPoint)
            {
                return new NetworkAddressKey
                (
                    IPv6Address: Messaging.IPAddressToBytes(ipEndPoint.Address).ToImmutableArray(),
                    Port: (UInt16)ipEndPoint.Port
                );
            }

            public static NetworkAddress ToNetworkAddress(this IPEndPoint ipEndPoint, UInt64 services)
            {
                return new NetworkAddress
                (
                    Services: services,
                    IPv6Address: Messaging.IPAddressToBytes(ipEndPoint.Address).ToImmutableArray(),
                    Port: (UInt16)ipEndPoint.Port
                );
            }

            public static CandidatePeer ToCandidatePeerKey(this IPEndPoint ipEndPoint)
            {
                return new CandidatePeer { IPEndPoint = ipEndPoint };
            }
        }
    }
}
