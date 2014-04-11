using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Script;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Core.Test.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ninject;
using NLog;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Test
{
    public class MainnetSimulator : IDisposable
    {
        private const UInt64 SATOSHI_PER_BTC = 100 * 1000 * 1000;

        private readonly Random random;
        private readonly MainnetBlockProvider blockProvider;
        private readonly IKernel kernel;
        private readonly Logger logger;
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly BlockCache blockCache;
        private readonly CoreDaemon coreDaemon;

        public MainnetSimulator()
        {
            this.random = new Random();
            this.blockProvider = new MainnetBlockProvider();

            // initialize kernel
            this.kernel = new StandardKernel();

            // add logging module
            this.kernel.Load(new ConsoleLoggingModule());

            // log startup
            this.logger = kernel.Get<Logger>();
            this.logger.Info("Starting up: {0}".Format2(DateTime.Now));

            // add storage module
            this.kernel.Load(new MemoryStorageModule());

            // add cache module
            this.kernel.Load(new CoreCacheModule());

            // initialize block view
            this.blockHeaderCache = this.kernel.Get<BlockHeaderCache>();
            this.blockCache = this.kernel.Get<BlockCache>();

            // add rules module
            this.kernel.Load(new RulesModule(RulesEnum.MainNet));

            // initialize the blockchain daemon
            this.kernel.Bind<CoreDaemon>().ToSelf().InSingletonScope();
            this.coreDaemon = this.kernel.Get<CoreDaemon>();

            // start the blockchain daemon
            this.coreDaemon.Start();

            // wait for initial work
            this.coreDaemon.ForceWorkAndWait();

            // verify initial state
            Assert.AreEqual(0, this.coreDaemon.TargetBlock.Height);
            //Assert.AreEqual(this.genesisBlock.Hash, this.blockchainDaemon.TargetChain.LastBlock.BlockHash);
            //Assert.AreEqual(this.genesisBlock.Hash, this.blockchainDaemon.ChainState.LastBlockHash);
        }

        public void Dispose()
        {
            this.kernel.Dispose();
        }

        public MainnetBlockProvider BlockProvider { get { return this.blockProvider; } }

        public IKernel Kernel { get { return this.kernel; } }

        public CoreDaemon CoreDaemon { get { return this.coreDaemon; } }

        public void AddBlockRange(int fromHeight, int toHeight)
        {
            for (var height = fromHeight; height <= toHeight; height++)
            {
                var block = this.blockProvider.GetBlock(height);
                AddBlock(block);
            }
        }

        public void AddBlock(int height)
        {
            var block = this.blockProvider.GetBlock(height);
            AddBlock(block);
        }

        public void AddBlock(UInt256 blockHash)
        {
            var block = this.blockProvider.GetBlock(blockHash.ToHexNumberString());
            AddBlock(block);
        }

        public void AddBlock(Block block)
        {
            this.blockHeaderCache[block.Hash] = block.Header;
            this.blockCache[block.Hash] = block;
        }

        public void WaitForDaemon()
        {
            var firedEvent = new AutoResetEvent(false);
            EventHandler onEvent = (sender, e) => firedEvent.Set();

            this.coreDaemon.OnTargetBlockChanged += onEvent;
            this.coreDaemon.OnTargetChainChanged += onEvent;
            this.coreDaemon.OnChainStateChanged += onEvent;
            this.coreDaemon.OnChainStateBuilderChanged += onEvent;
            try
            {
                while (firedEvent.WaitOne(500)) { }
            }
            finally
            {
                this.coreDaemon.OnTargetBlockChanged -= onEvent;
                this.coreDaemon.OnTargetChainChanged -= onEvent;
                this.coreDaemon.OnChainStateChanged -= onEvent;
                this.coreDaemon.OnChainStateBuilderChanged -= onEvent;
            }
        }

        public void CloseChainStateBuiler()
        {
            var original = this.coreDaemon.MaxBuilderTime;
            try
            {
                this.coreDaemon.MaxBuilderTime = TimeSpan.Zero;
                this.coreDaemon.ForceWorkAndWait();
            }
            finally
            {
                this.coreDaemon.MaxBuilderTime = original;
            }
        }
    }
}
