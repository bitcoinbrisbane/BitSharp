using BitSharp.Common;
using BitSharp.Data;
using Ninject;
using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Esent
{
    public class EsentStorageModule : NinjectModule
    {
        public override void Load()
        {
            // bind storage providers
            Bind<IBlockHeaderStorage>().To<BlockHeaderStorage>();
            Bind<IChainedBlockStorage>().To<ChainedBlockStorage>();
            Bind<IBlockTxHashesStorage>().To<BlockTxHashesStorage>();
            Bind<ITransactionStorage>().To<TransactionStorage>();
            Bind<IBlockRollbackStorage>().To<BlockRollbackStorage>();
            Bind<IInvalidBlockStorage>().To<InvalidBlockStorage>();
            Bind<IUtxoBuilderStorage>().To<UtxoBuilderStorage>();
        }
    }
}
