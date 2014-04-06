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
    }
}
