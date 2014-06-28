using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Node.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    public class EsentStorageModule : NinjectModule
    {
        private readonly string baseDirectory;
        private readonly long? cacheSizeMinBytes;
        private readonly long? cacheSizeMaxBytes;
        private readonly bool transientBlockStorage;

        public EsentStorageModule(string baseDirectory, long? cacheSizeMinBytes = null, long? cacheSizeMaxBytes = null, bool transientBlockStorage = false)
        {
            this.baseDirectory = baseDirectory;
            this.cacheSizeMinBytes = cacheSizeMinBytes;
            this.cacheSizeMaxBytes = cacheSizeMaxBytes;
            this.transientBlockStorage = transientBlockStorage;
        }

        public override void Load()
        {
            //TODO remove reflection once PersistentDictionary is phased out
            var esentAssembly = typeof(PersistentDictionary<string, string>).Assembly;
            var type = esentAssembly.GetType("Microsoft.Isam.Esent.Collections.Generic.CollectionsSystemParameters");
            var method = type.GetMethod("Init");
            method.Invoke(null, null);
            if (this.cacheSizeMinBytes != null)
                SystemParameters.CacheSizeMin = (this.cacheSizeMinBytes.Value / SystemParameters.DatabasePageSize).ToIntChecked();
            if (this.cacheSizeMaxBytes != null)
                SystemParameters.CacheSizeMax = (this.cacheSizeMaxBytes.Value / SystemParameters.DatabasePageSize).ToIntChecked();

            // bind concrete storage providers
            this.Bind<BlockHeaderStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            this.Bind<ChainedHeaderStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            //this.Bind<BlockStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            if (this.transientBlockStorage)
            {
                this.Bind<MemoryBlockTxHashesStorage>().ToSelf().InSingletonScope();
                this.Bind<MemoryTransactionStorage>().ToSelf().InSingletonScope();
            }
            else
            {
                this.Bind<BlockTxHashesStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
                this.Bind<TransactionStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            }
            this.Bind<SpentTransactionsStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            this.Bind<SpentOutputsStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            this.Bind<InvalidBlockStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            this.Bind<NetworkPeerStorage>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            this.Bind<BlockStorageNew>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);

            // bind storage providers interfaces
            this.Bind<IBlockHeaderStorage>().ToMethod(x => this.Kernel.Get<BlockHeaderStorage>()).InSingletonScope();
            this.Bind<IChainedHeaderStorage>().ToMethod(x => this.Kernel.Get<ChainedHeaderStorage>()).InSingletonScope();
            //this.Bind<IBlockStorage>().ToMethod(x => this.Kernel.Get<BlockStorage>()).InSingletonScope();
            if (this.transientBlockStorage)
            {
                this.Bind<IBlockTxHashesStorage>().ToMethod(x => this.Kernel.Get<MemoryBlockTxHashesStorage>()).InSingletonScope();
                this.Bind<ITransactionStorage>().ToMethod(x => this.Kernel.Get<MemoryTransactionStorage>()).InSingletonScope();
            }
            else
            {
                this.Bind<IBlockTxHashesStorage>().ToMethod(x => this.Kernel.Get<BlockTxHashesStorage>()).InSingletonScope();
                this.Bind<ITransactionStorage>().ToMethod(x => this.Kernel.Get<TransactionStorage>()).InSingletonScope();
            }
            this.Bind<ISpentTransactionsStorage>().ToMethod(x => this.Kernel.Get<SpentTransactionsStorage>()).InSingletonScope();
            this.Bind<ISpentOutputsStorage>().ToMethod(x => this.Kernel.Get<SpentOutputsStorage>()).InSingletonScope();
            this.Bind<IInvalidBlockStorage>().ToMethod(x => this.Kernel.Get<InvalidBlockStorage>()).InSingletonScope();
            this.Bind<INetworkPeerStorage>().ToMethod(x => this.Kernel.Get<NetworkPeerStorage>()).InSingletonScope();
            this.Bind<IBlockStorageNew>().ToMethod(x => this.Kernel.Get<BlockStorageNew>()).InSingletonScope();

            //this.Bind<IChainStateBuilderStorage>().To<ChainStateBuilderStorage>().InTransientScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            this.Bind<IChainStateBuilderStorage>().To<MemoryChainStateBuilderStorage>().InTransientScope();
        }
    }
}
