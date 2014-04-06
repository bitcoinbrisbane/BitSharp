using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage.Memory;
using BitSharp.Node.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Node.Storage.Memory
{
    public sealed class MemoryNetworkPeerStorage : MemoryStorage<NetworkAddressKey, NetworkAddressWithTime>, INetworkPeerStorage { }
}
