using BitSharp.Common;
using BitSharp.Core.Storage;
using BitSharp.Node.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Node.Storage
{
    public interface INetworkPeerStorage :
        IBoundedStorage<NetworkAddressKey, NetworkAddressWithTime> { }

    public sealed class NetworkPeerCache : PassthroughBoundedCache<NetworkAddressKey, NetworkAddressWithTime>
    {
        public NetworkPeerCache(IBoundedCache<NetworkAddressKey, NetworkAddressWithTime> cache)
            : base(cache) { }
    }
}
