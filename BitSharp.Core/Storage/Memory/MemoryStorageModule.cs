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

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryStorageModule : NinjectModule
    {
        public override void Load()
        {
            // bind concrete storage providers
            this.Bind<MemoryBlockHeaderStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryChainedHeaderStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryBlockStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryBlockTxHashesStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryTransactionStorage>().ToSelf().InSingletonScope();
            this.Bind<MemorySpentTransactionsStorage>().ToSelf().InSingletonScope();
            this.Bind<MemorySpentOutputsStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryInvalidBlockStorage>().ToSelf().InSingletonScope();

            // bind storage providers interfaces
            this.Bind<IBlockHeaderStorage>().ToMethod(x => this.Kernel.Get<MemoryBlockHeaderStorage>()).InSingletonScope();
            this.Bind<IChainedHeaderStorage>().ToMethod(x => this.Kernel.Get<MemoryChainedHeaderStorage>()).InSingletonScope();
            this.Bind<IBlockStorage>().ToMethod(x => this.Kernel.Get<MemoryBlockStorage>()).InSingletonScope();
            this.Bind<IBlockTxHashesStorage>().ToMethod(x => this.Kernel.Get<MemoryBlockTxHashesStorage>()).InSingletonScope();
            this.Bind<ITransactionStorage>().ToMethod(x => this.Kernel.Get<MemoryTransactionStorage>()).InSingletonScope();
            this.Bind<ISpentTransactionsStorage>().ToMethod(x => this.Kernel.Get<MemorySpentTransactionsStorage>()).InSingletonScope();
            this.Bind<ISpentOutputsStorage>().ToMethod(x => this.Kernel.Get<MemorySpentOutputsStorage>()).InSingletonScope();
            this.Bind<IInvalidBlockStorage>().ToMethod(x => this.Kernel.Get<MemoryInvalidBlockStorage>()).InSingletonScope();

            this.Bind<IUtxoBuilderStorage>().To<MemoryUtxoBuilderStorage>();
        }
    }
}
