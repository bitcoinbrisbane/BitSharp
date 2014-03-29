using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Network;
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
            this.Bind<MemoryInvalidBlockStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryBlockRollbackStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryNetworkPeerStorage>().ToSelf().InSingletonScope();

            // bind storage provider interfaces
            this.Bind<IBlockHeaderStorage>().To<MemoryBlockHeaderStorage>().InSingletonScope();
            this.Bind<IChainedBlockStorage>().To<MemoryChainedBlockStorage>().InSingletonScope();
            this.Bind<IBlockTxHashesStorage>().To<MemoryBlockTxHashesStorage>().InSingletonScope();
            this.Bind<ITransactionStorage>().To<MemoryTransactionStorage>().InSingletonScope();
            this.Bind<IInvalidBlockStorage>().To<MemoryInvalidBlockStorage>().InSingletonScope();
            this.Bind<IBlockRollbackStorage>().To<MemoryBlockRollbackStorage>().InSingletonScope();
            this.Bind<INetworkPeerStorage>().To<MemoryNetworkPeerStorage>().InSingletonScope();
        }
    }
}
