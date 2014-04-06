using AustinHarris.JsonRpc;
using BitSharp.Core;
using BitSharp.Core.JsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Node
{
    public class NodeRpcServer : CoreRpcServer
    {
        private readonly CoreDaemon coreDaemon;

        public NodeRpcServer(CoreDaemon coreDaemon)
            : base(coreDaemon)
        {
            this.coreDaemon = coreDaemon;
        }
    }
}
