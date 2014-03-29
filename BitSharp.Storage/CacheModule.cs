using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using BitSharp.Network;
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

namespace BitSharp.Storage
{
    public class CacheModule : NinjectModule
    {
        private IBoundedCache<UInt256, BlockHeader> blockHeaderCache;
        private IBoundedCache<UInt256, ChainedBlock> chainedBlockCache;
        private IBoundedCache<UInt256, IImmutableList<UInt256>> blockTxHashesCache;
        private IUnboundedCache<UInt256, Transaction> transactionCache;
        private IBoundedCache<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>> blockRollbackCache;
        private IBoundedCache<UInt256, string> invalidBlockCache;
        private IBoundedCache<NetworkAddressKey, NetworkAddressWithTime> networkPeerCache;

        public override void Load()
        {
            var blockHeaderStorage = this.Kernel.Get<IBlockHeaderStorage>();
            this.blockHeaderCache = this.Kernel.Get<BoundedFullCache<UInt256, BlockHeader>>(
                new ConstructorArgument("name", "Block Header Cache"), new ConstructorArgument("dataStorage", blockHeaderStorage));

            var chainedBlockStorage = this.Kernel.Get<IChainedBlockStorage>();
            this.chainedBlockCache = this.Kernel.Get<BoundedFullCache<UInt256, ChainedBlock>>(
                new ConstructorArgument("name", "Chained Block Cache"), new ConstructorArgument("dataStorage", chainedBlockStorage));

            var blockTxHashesStorage = this.Kernel.Get<IBlockTxHashesStorage>();
            this.blockTxHashesCache = this.Kernel.Get<BoundedCache<UInt256, IImmutableList<UInt256>>>(
                new ConstructorArgument("name", "Block TX Hashes Cache"), new ConstructorArgument("dataStorage", blockTxHashesStorage));

            var transactionStorage = this.Kernel.Get<ITransactionStorage>();
            this.transactionCache = this.Kernel.Get<UnboundedCache<UInt256, Transaction>>(
                new ConstructorArgument("name", "Transaction Cache"), new ConstructorArgument("dataStorage", transactionStorage));

            var blockRollbackStorage = this.Kernel.Get<IBlockRollbackStorage>();
            this.blockRollbackCache = this.Kernel.Get<BoundedCache<UInt256, IImmutableList<KeyValuePair<UInt256, UInt256>>>>(
                new ConstructorArgument("name", "Block Rollback Cache"), new ConstructorArgument("dataStorage", blockRollbackStorage));

            var invalidBlockStorage = this.Kernel.Get<IInvalidBlockStorage>();
            this.invalidBlockCache = this.Kernel.Get<BoundedCache<UInt256, string>>(
                new ConstructorArgument("name", "Invalid Block Cache"), new ConstructorArgument("dataStorage", invalidBlockStorage));

            var networkPeerStroage = this.Kernel.Get<INetworkPeerStorage>();
            this.networkPeerCache = this.Kernel.Get<BoundedCache<NetworkAddressKey, NetworkAddressWithTime>>(
                new ConstructorArgument("name", "Network Peer Cache"), new ConstructorArgument("dataStorage", networkPeerStroage));

            this.Bind<BlockHeaderCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.blockHeaderCache);
            this.Bind<ChainedBlockCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.chainedBlockCache);
            this.Bind<BlockTxHashesCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.blockTxHashesCache);
            this.Bind<TransactionCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.transactionCache);
            this.Bind<BlockRollbackCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.blockRollbackCache);
            this.Bind<InvalidBlockCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.invalidBlockCache);
            this.Bind<NetworkPeerCache>().ToSelf().InSingletonScope().WithConstructorArgument(this.networkPeerCache);
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
