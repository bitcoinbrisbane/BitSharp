using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using Ninject;
using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Node.Storage.Memory
{
    public class MemoryStorageModule : NinjectModule
    {
        public override void Load()
        {
            // bind concrete storage providers
            this.Bind<MemoryNetworkPeerStorage>().ToSelf().InSingletonScope();

            // bind storage providers interfaces
            this.Bind<INetworkPeerStorage>().ToMethod(x => this.Kernel.Get<MemoryNetworkPeerStorage>()).InSingletonScope();
        }
    }
}
