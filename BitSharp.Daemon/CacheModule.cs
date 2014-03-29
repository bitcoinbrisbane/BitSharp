using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Storage;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Daemon
{
    public class CacheModule : NinjectModule
    {
        private BoundedFullCache<UInt256, BlockHeader> blockHeaderCache;
        private BoundedFullCache<UInt256, ChainedBlock> chainedBlockCache;
        private BoundedCache<UInt256, IImmutableList<UInt256>> blockTxHashesCache;
        private UnboundedCache<UInt256, Transaction> transactionCache;
        private BoundedCache<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>> blockRollbackCache;
        private BoundedCache<UInt256, string> invalidBlockCache;

        public override void Load()
        {
            this.blockHeaderCache = this.Kernel.Get<BoundedFullCache<UInt256, BlockHeader>>("Block Header Cache");
            this.chainedBlockCache = this.Kernel.Get<BoundedFullCache<UInt256, ChainedBlock>>("Chained Block Cache");
            this.blockTxHashesCache = this.Kernel.Get<BoundedCache<UInt256, IImmutableList<UInt256>>>("Block TX Hashes Cache");
            this.transactionCache = this.Kernel.Get<UnboundedCache<UInt256, Transaction>>("Transaction Cache");
            this.blockRollbackCache = this.Kernel.Get<BoundedCache<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>>>("Block Rollback Cache");
            this.invalidBlockCache = this.Kernel.Get<BoundedCache<UInt256, string>>("Invalid Block Cache");

            this.Bind<BlockHeaderCache>().ToSelf().WithConstructorArgument(this.blockHeaderCache);
            this.Bind<ChainedBlockCache>().ToSelf().WithConstructorArgument(this.chainedBlockCache);
            this.Bind<BlockTxHashesCache>().ToSelf().WithConstructorArgument(this.blockTxHashesCache);
            this.Bind<TransactionCache>().ToSelf().WithConstructorArgument(this.transactionCache);
            this.Bind<BlockRollbackCache>().ToSelf().WithConstructorArgument(this.blockRollbackCache);
            this.Bind<InvalidBlockCache>().ToSelf().WithConstructorArgument(this.invalidBlockCache);
        }

        public override void Unload()
        {
            new IDisposable[]
            {
                this.blockHeaderCache,
                this.chainedBlockCache,
                this.blockTxHashesCache,
                this.transactionCache,
                this.blockRollbackCache,
                this.invalidBlockCache
            }
            .DisposeList();

            base.Unload();
        }
    }
}
