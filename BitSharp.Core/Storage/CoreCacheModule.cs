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
                new ConstructorArgument("name", "Chained Header Cache"), new ConstructorArgument("dataStorage", chainedHeaderStorage));

            var invalidBlockStorage = this.Kernel.Get<IInvalidBlockStorage>();
            this.invalidBlockCache = this.Kernel.Get<BoundedCache<UInt256, string>>(
                new ConstructorArgument("name", "Invalid Block Cache"), new ConstructorArgument("dataStorage", invalidBlockStorage));

            this.Bind<BlockHeaderCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.blockHeaderCache);
            this.Bind<ChainedHeaderCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.chainedHeaderCache);
            this.Bind<InvalidBlockCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.invalidBlockCache);
        }

        public override void Unload()
        {
            new IDisposable[]
            {
                this.blockHeaderCache,
                this.chainedHeaderCache,
                this.spentTransactionsCache,
                this.spentOutputsCache,
                this.invalidBlockCache
            }
            .DisposeList();

            base.Unload();
        }
    }
}
