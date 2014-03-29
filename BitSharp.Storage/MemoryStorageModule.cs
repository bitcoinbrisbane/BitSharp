using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Network;
using Ninject;
using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class MemoryStorageModule : NinjectModule
    {
        public override void Load()
        {
            // bind concrete storage providers
            this.Bind<MemoryBlockHeaderStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryChainedBlockStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryBlockTxHashesStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryTransactionStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryBlockRollbackStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryInvalidBlockStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryNetworkPeerStorage>().ToSelf().InSingletonScope();

            // bind storage providers interfaces
            this.Bind<IBlockHeaderStorage>().ToMethod(x => this.Kernel.Get<MemoryBlockHeaderStorage>()).InSingletonScope();
            this.Bind<IChainedBlockStorage>().ToMethod(x => this.Kernel.Get<MemoryChainedBlockStorage>()).InSingletonScope();
            this.Bind<IBlockTxHashesStorage>().ToMethod(x => this.Kernel.Get<MemoryBlockTxHashesStorage>()).InSingletonScope();
            this.Bind<ITransactionStorage>().ToMethod(x => this.Kernel.Get<MemoryTransactionStorage>()).InSingletonScope();
            this.Bind<IBlockRollbackStorage>().ToMethod(x => this.Kernel.Get<MemoryBlockRollbackStorage>()).InSingletonScope();
            this.Bind<IInvalidBlockStorage>().ToMethod(x => this.Kernel.Get<MemoryInvalidBlockStorage>()).InSingletonScope();
            this.Bind<INetworkPeerStorage>().ToMethod(x => this.Kernel.Get<MemoryNetworkPeerStorage>()).InSingletonScope();

            this.Bind<IUtxoBuilderStorage>().To<MemoryUtxoBuilderStorage>();
        }
    }
}
