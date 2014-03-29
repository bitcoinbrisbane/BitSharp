using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage.Esent;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Esent
{
    public class MixedStorageModule : NinjectModule
    {
        private readonly string baseDirectory;
        private readonly long cacheSizeMaxBytes;

        public MixedStorageModule(string baseDirectory, long cacheSizeMaxBytes)
        {
            this.baseDirectory = baseDirectory;
            this.cacheSizeMaxBytes = cacheSizeMaxBytes;
        }

        public override void Load()
        {
            var esentAssembly = typeof(PersistentDictionary<string, string>).Assembly;
            var type = esentAssembly.GetType("Microsoft.Isam.Esent.Collections.Generic.CollectionsSystemParameters");
            var method = type.GetMethod("Init");
            method.Invoke(null, null);
            SystemParameters.CacheSizeMax = (cacheSizeMaxBytes / SystemParameters.DatabasePageSize).ToIntChecked();

            // bind concrete storage providers
            this.Bind<BlockHeaderStorage>().ToSelf().InSingletonScope();
            this.Bind<ChainedBlockStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryBlockTxHashesStorage>().ToSelf().InSingletonScope();
            this.Bind<MemoryTransactionStorage>().ToSelf().InSingletonScope();
            this.Bind<BlockRollbackStorage>().ToSelf().InSingletonScope();
            this.Bind<InvalidBlockStorage>().ToSelf().InSingletonScope();
            this.Bind<UtxoBuilderStorage>().ToSelf().InSingletonScope();

            // bind storage providers
            this.Bind<IBlockHeaderStorage>().To<BlockHeaderStorage>().InSingletonScope();
            this.Bind<IChainedBlockStorage>().To<ChainedBlockStorage>().InSingletonScope();
            this.Bind<IBlockTxHashesStorage>().To<MemoryBlockTxHashesStorage>().InSingletonScope(); // in-memory
            this.Bind<ITransactionStorage>().To<MemoryTransactionStorage>().InSingletonScope(); // in-memory
            this.Bind<IBlockRollbackStorage>().To<BlockRollbackStorage>().InSingletonScope();
            this.Bind<IInvalidBlockStorage>().To<InvalidBlockStorage>().InSingletonScope();
            this.Bind<IUtxoBuilderStorage>().To<UtxoBuilderStorage>().InSingletonScope();
        }
    }
}
