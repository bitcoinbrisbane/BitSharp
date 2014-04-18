using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public class CoreCacheModule : NinjectModule
    {
        private IBoundedCache<UInt256, BlockHeader> blockHeaderCache;
        private IBoundedCache<UInt256, ChainedHeader> chainedHeaderCache;
        private IBoundedCache<UInt256, Block> blockCache;
        private IBoundedCache<UInt256, IImmutableList<UInt256>> blockTxHashesCache;
        private IUnboundedCache<UInt256, Transaction> transactionCache;
        private IBoundedCache<UInt256, IImmutableList<KeyValuePair<UInt256, SpentTx>>> spentTransactionsCache;
        private IBoundedCache<UInt256, IImmutableList<KeyValuePair<TxOutputKey, TxOutput>>> spentOutputsCache;
        private IBoundedCache<UInt256, string> invalidBlockCache;

        public override void Load()
        {
            var blockHeaderStorage = this.Kernel.Get<IBlockHeaderStorage>();
            this.blockHeaderCache = this.Kernel.Get<BoundedFullCache<UInt256, BlockHeader>>(
                new ConstructorArgument("name", "Block Header Cache"), new ConstructorArgument("dataStorage", blockHeaderStorage));

            var chainedHeaderStorage = this.Kernel.Get<IChainedHeaderStorage>();
            this.chainedHeaderCache = this.Kernel.Get<BoundedFullCache<UInt256, ChainedHeader>>(
                new ConstructorArgument("name", "Chained Block Cache"), new ConstructorArgument("dataStorage", chainedHeaderStorage));

            var blockStorage = this.Kernel.Get<IBlockStorage>();
            this.blockCache = this.Kernel.Get<BoundedCache<UInt256, Block>>(
                new ConstructorArgument("name", "Block Cache"), new ConstructorArgument("dataStorage", blockStorage));

            var blockTxHashesStorage = this.Kernel.Get<IBlockTxHashesStorage>();
            this.blockTxHashesCache = this.Kernel.Get<BoundedCache<UInt256, IImmutableList<UInt256>>>(
                new ConstructorArgument("name", "Block TX Hashes Cache"), new ConstructorArgument("dataStorage", blockTxHashesStorage));

            var transactionStorage = this.Kernel.Get<ITransactionStorage>();
            this.transactionCache = this.Kernel.Get<UnboundedCache<UInt256, Transaction>>(
                new ConstructorArgument("name", "Transaction Cache"), new ConstructorArgument("dataStorage", transactionStorage));

            var spentTransactionsStorage = this.Kernel.Get<ISpentTransactionsStorage>();
            this.spentTransactionsCache = this.Kernel.Get<BoundedCache<UInt256, IImmutableList<KeyValuePair<UInt256, SpentTx>>>>(
                new ConstructorArgument("name", "Spent Transactions Cache"), new ConstructorArgument("dataStorage", spentTransactionsStorage));

            var spentOutputsStorage = this.Kernel.Get<ISpentOutputsStorage>();
            this.spentOutputsCache = this.Kernel.Get<BoundedCache<UInt256, IImmutableList<KeyValuePair<TxOutputKey, TxOutput>>>>(
                new ConstructorArgument("name", "Spent Outputs Cache"), new ConstructorArgument("dataStorage", spentOutputsStorage));

            var invalidBlockStorage = this.Kernel.Get<IInvalidBlockStorage>();
            this.invalidBlockCache = this.Kernel.Get<BoundedCache<UInt256, string>>(
                new ConstructorArgument("name", "Invalid Block Cache"), new ConstructorArgument("dataStorage", invalidBlockStorage));

            this.Bind<BlockHeaderCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.blockHeaderCache);
            this.Bind<ChainedHeaderCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.chainedHeaderCache);
            this.Bind<BlockCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.blockCache);
            this.Bind<BlockTxHashesCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.blockTxHashesCache);
            this.Bind<TransactionCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.transactionCache);
            this.Bind<SpentTransactionsCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.spentTransactionsCache);
            this.Bind<SpentOutputsCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.spentOutputsCache);
            this.Bind<InvalidBlockCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.invalidBlockCache);

            if (false)
            {
                this.blockCache = this.Kernel.Get<BlockCompositeCache>();
                this.Bind<BlockCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.blockCache);
            }
        }

        public override void Unload()
        {
            new IDisposable[]
            {
                this.blockHeaderCache,
                this.chainedHeaderCache,
                this.blockCache,
                this.blockTxHashesCache,
                this.transactionCache,
                this.spentTransactionsCache,
                this.invalidBlockCache
            }
            .DisposeList();

            base.Unload();
        }
    }
}
