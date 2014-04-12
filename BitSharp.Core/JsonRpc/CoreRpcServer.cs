using AustinHarris.JsonRpc;
using BitSharp.Common;
using BitSharp.Core;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.JsonRpc
{
    //TODO the reference implementation has the chain information, network information, and wallet information all running under one RPC service
    //TODO i'm not touching private keys, so all of the wallet commands will be for monitoring
    //TODO i'll have to add something non-standard to tell it what addresses to watch, so i can use standard commands like "getreceivedbyaddress"
    public class CoreRpcServer : JsonRpcService, IDisposable
    {
        private readonly Logger logger;
        private readonly CoreDaemon coreDaemon;
        private readonly ListenerWorker listener;

        public CoreRpcServer(Logger logger, CoreDaemon coreDaemon)
        {
            this.logger = logger;
            this.coreDaemon = coreDaemon;
            this.listener = new ListenerWorker(this.logger);
        }

        public void Dispose()
        {
            this.listener.Dispose();
        }

        public void StartListening()
        {
            this.listener.Start();
        }

        public void StopListening()
        {
            this.listener.Stop();
        }

        [JsonRpcMethod("getblock")]
        public void GetBlock(UInt256 blockHash)
        {
        }

        [JsonRpcMethod("getreceivedbyaddress")]
        public void GetReceivedByAddress(string address, int minConf)
        {
        }

        private sealed class ListenerWorker : Worker
        {
            private readonly Logger logger;
            private TcpListener server;

            public ListenerWorker(Logger logger)
                : base("CoreRpcServer.ListenerWorker", initialNotify: true, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.Zero, logger: logger)
            {
                this.logger = logger;
            }

            protected override void SubStart()
            {
                if (this.server == null)
                    this.server = new TcpListener(IPAddress.Parse("127.0.0.1"), 8332);

                this.server.Start();
            }

            protected override void SubStop()
            {
                if (this.server != null)
                    this.server.Stop();
            }

            protected override void WorkAction()
            {
                try
                {
                    using (var client = this.server.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        var reader = new StreamReader(stream, Encoding.UTF8);
                        var writer = new StreamWriter(stream, new UTF8Encoding(false));

                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            Debug.WriteLine(line);
                            var async = new JsonRpcStateAsync(RpcResultHandler, writer) { JsonRpc = line };
                            JsonRpcProcessor.Process(async, writer);
                        }
                    }
                }
                finally
                {
                    // always notify to continue accepting connections
                    this.NotifyWork();
                }
            }

            private void RpcResultHandler(IAsyncResult state)
            {
                var async = ((JsonRpcStateAsync)state);
                var result = async.Result;
                var writer = ((StreamWriter)async.AsyncState);

                writer.WriteLine(result);
            }
        }
    }
}
