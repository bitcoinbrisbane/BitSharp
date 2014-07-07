using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Node.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    public class EsentStorageManager : IStorageManager
    {
        private readonly Logger logger;
        private readonly string baseDirectory;
        private readonly IBlockHeaderStorage blockHeaderStorage;
        private readonly IChainedHeaderStorage chainedHeaderStorage;
        private readonly IInvalidBlockStorage invalidBlockStorage;
        private readonly IBlockStorageNew blockStorage;
        private readonly EsentChainStateManager chainStateManager;

        public EsentStorageManager(string baseDirectory, Logger logger)
        {
            this.logger = logger;
            this.baseDirectory = baseDirectory;

            try
            {
                this.blockHeaderStorage = new BlockHeaderStorage(this.baseDirectory);
                this.chainedHeaderStorage = new ChainedHeaderStorage(this.baseDirectory);
                this.invalidBlockStorage = new InvalidBlockStorage(this.baseDirectory);
                this.blockStorage = new BlockStorageNew(this.baseDirectory);
                this.chainStateManager = new EsentChainStateManager(this.baseDirectory, this.logger);
            }
            catch (Exception)
            {
                // ensure any storage that was opened during construction gets closed on an error
                this.Dispose();

                throw;
            }
        }

        public void Dispose()
        {
            new IDisposable[] {
                this.blockHeaderStorage,
                this.chainedHeaderStorage,
                this.invalidBlockStorage,
                this.blockStorage,
                this.chainStateManager
            }.DisposeList();
        }

        public IBlockHeaderStorage BlockHeaderStorage
        {
            get { return this.blockHeaderStorage; }
        }

        public IChainedHeaderStorage ChainedHeaderStorage
        {
            get { return this.chainedHeaderStorage; }
        }

        public IInvalidBlockStorage InvalidBlockStorage
        {
            get { return this.invalidBlockStorage; }
        }

        public IBlockStorageNew BlockStorage
        {
            get { return this.blockStorage; }
        }

        public IChainStateBuilderStorage CreateOrLoadChainState(ChainedHeader genesisHeader)
        {
            return this.chainStateManager.CreateOrLoadChainState(genesisHeader);
        }
    }
}
