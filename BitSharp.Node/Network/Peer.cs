using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using System.Collections.Immutable;
using BitSharp.Common;
using NLog;
using BitSharp.Node.Domain;

namespace BitSharp.Node.Network
{
    public class Peer : IDisposable
    {
        public event Action<Peer, GetBlocksPayload> OnGetBlocks;
        public event Action<Peer, GetBlocksPayload> OnGetHeaders;
        public event Action<Peer, InventoryPayload> OnGetData;
        public event Action<Peer, ImmutableArray<byte>> OnPing;
        public event Action<Peer> OnDisconnect;

        private readonly Logger logger;
        //TODO semaphore not disposed
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private int disposeCount;

        private bool startedConnecting = false;
        private bool isConnected = false;
        private /*readonly*/ IPEndPoint localEndPoint;
        private readonly IPEndPoint remoteEndPoint;
        private readonly Socket socket;
        private readonly RemoteReceiver receiver;
        private readonly RemoteSender sender;
        private readonly bool isSeed;

        private CountMeasure blockMissCountMeasure;

        public Peer(IPEndPoint remoteEndPoint, bool isSeed, Logger logger)
        {
            this.logger = logger;
            this.remoteEndPoint = remoteEndPoint;
            this.isSeed = isSeed;

            this.socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.receiver = new RemoteReceiver(this, this.socket, persistent: false, logger: this.logger);
            this.sender = new RemoteSender(this.socket, this.logger);

            this.blockMissCountMeasure = new CountMeasure(TimeSpan.FromMinutes(10));

            WireNode();
        }

        public Peer(Socket socket, bool isSeed, Logger logger)
        {
            this.logger = logger;
            this.socket = socket;
            this.isConnected = true;

            this.localEndPoint = (IPEndPoint)socket.LocalEndPoint;
            this.remoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;

            this.receiver = new RemoteReceiver(this, this.socket, persistent: false, logger: this.logger);
            this.sender = new RemoteSender(this.socket, this.logger);

            this.blockMissCountMeasure = new CountMeasure(TimeSpan.FromMinutes(10));

            WireNode();
        }

        ~Peer()
        {
            Dispose();
        }

        public void Dispose()
        {
            // already disposed, only dispose once
            if (Interlocked.Increment(ref this.disposeCount) != 1)
                return;
            
            GC.SuppressFinalize(this);

            UnwireNode();

            try
            {
                this.socket.Dispose();
            }
            catch (Exception) { }

            if (this.startedConnecting || this.isConnected)
            {
                this.startedConnecting = false;
                this.isConnected = false;

                var handler = this.OnDisconnect;
                if (handler != null)
                    handler(this);
            }

            this.blockMissCountMeasure.Dispose();
        }

        public IPEndPoint LocalEndPoint { get { return this.localEndPoint; } }

        public IPEndPoint RemoteEndPoint { get { return this.remoteEndPoint; } }

        public RemoteReceiver Receiver { get { return this.receiver; } }

        public RemoteSender Sender { get { return this.sender; } }

        public bool IsConnected { get { return this.isConnected; } }

        public bool IsSeed { get { return this.isSeed; } }

        public int BlockMissCount { get { return this.blockMissCountMeasure.GetCount(); } }

        public void AddBlockMiss()
        {
            this.blockMissCountMeasure.Tick();
        }

        public async Task ConnectAsync()
        {
            await semaphore.DoAsync(async () =>
            {
                try
                {
                    if (!IsConnected)
                    {
                        this.startedConnecting = true;

                        await Task.Factory.FromAsync(this.socket.BeginConnect(this.remoteEndPoint, null, null), this.socket.EndConnect);

                        this.localEndPoint = (IPEndPoint)this.socket.LocalEndPoint;

                        this.isConnected = true;
                    }
                }
                catch (Exception e)
                {
                    this.logger.Debug(string.Format("Error on connecting to {0}", remoteEndPoint), e);
                    Disconnect();
                }
            });
        }

        public void Disconnect()
        {
            this.Dispose();
        }

        private void WireNode()
        {
            this.receiver.OnFailed += HandleFailed;
            this.sender.OnFailed += HandleFailed;
            this.receiver.OnGetBlocks += HandleGetBlocks;
            this.receiver.OnGetHeaders += HandleGetHeaders;
            this.receiver.OnGetData += HandleGetData;
            this.receiver.OnPing += HandlePing;
        }

        private void UnwireNode()
        {
            this.receiver.OnFailed -= HandleFailed;
            this.sender.OnFailed -= HandleFailed;
            this.receiver.OnGetBlocks -= HandleGetBlocks;
            this.receiver.OnGetHeaders -= HandleGetHeaders;
            this.receiver.OnGetData -= HandleGetData;
            this.receiver.OnPing -= HandlePing;
        }

        private void HandleFailed(Exception e)
        {
            if (e != null)
                this.logger.Debug("Remote peer failed: {0}".Format2(this.remoteEndPoint), e);
            else
                this.logger.Debug("Remote peer failed: {0}".Format2(this.remoteEndPoint));

            Disconnect();
        }

        private void HandleGetBlocks(GetBlocksPayload payload)
        {
            var handler = this.OnGetBlocks;
            if (handler != null)
                handler(this, payload);
        }

        private void HandleGetHeaders(GetBlocksPayload payload)
        {
            var handler = this.OnGetHeaders;
            if (handler != null)
                handler(this, payload);
        }

        private void HandleGetData(InventoryPayload payload)
        {
            var handler = this.OnGetData;
            if (handler != null)
                handler(this, payload);
        }

        private void HandlePing(ImmutableArray<byte> payload)
        {
            var handler = this.OnPing;
            if (handler != null)
                handler(this, payload);
        }
    }
}
