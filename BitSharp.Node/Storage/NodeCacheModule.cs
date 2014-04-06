using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Node.Domain;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Node.Storage
{
    public class NodeCacheModule : NinjectModule
    {
        private IBoundedCache<NetworkAddressKey, NetworkAddressWithTime> networkPeerCache;

        public override void Load()
        {
            var networkPeerStorage = this.Kernel.Get<INetworkPeerStorage>();
            this.networkPeerCache = this.Kernel.Get<BoundedCache<NetworkAddressKey, NetworkAddressWithTime>>(
                new ConstructorArgument("name", "Network Peer Cache"), new ConstructorArgument("dataStorage", networkPeerStorage));

            this.Bind<NetworkPeerCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.networkPeerCache);
        }

        public override void Unload()
        {
            new IDisposable[]
            {
                this.networkPeerCache
            }
            .DisposeList();

            base.Unload();
        }
    }
}
