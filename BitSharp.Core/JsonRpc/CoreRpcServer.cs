using AustinHarris.JsonRpc;
using BitSharp.Common;
using BitSharp.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.JsonRpc
{
    //TODO the reference implementation has the chain information, network information, and wallet information all running under one RPC service
    //TODO i'm not touching private keys, so all of the wallet commands will be for monitoring
    //TODO i'll have to add something non-standard to tell it what addresses to watch, so i can use standard commands like "getreceivedbyaddress"
    public class CoreRpcServer : JsonRpcService
    {
        private readonly CoreDaemon coreDaemon;

        public CoreRpcServer(CoreDaemon coreDaemon)
        {
            this.coreDaemon = coreDaemon;
        }

        [JsonRpcMethod("getblock")]
        public void GetBlock(UInt256 blockHash)
        {
        }

        [JsonRpcMethod("getreceivedbyaddress")]
        public void GetReceivedByAddress(string address, int minConf)
        {
        }
    }
}
