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
        private readonly BlockStorage blockStorage;
        private readonly BlockTxesStorage blockTxesStorage;
        private readonly EsentChainStateManager chainStateManager;

        public EsentStorageManager(string baseDirectory, Logger logger)
        {
            this.logger = logger;
            this.baseDirectory = baseDirectory;

            try
            {
                this.blockTxesStorage = new BlockTxesStorage(this.baseDirectory, this.logger);
                this.blockStorage = new BlockStorage(this.baseDirectory, this.blockTxesStorage.JetInstance, this.logger);
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
                this.chainStateManager,
                this.blockStorage,
                this.blockTxesStorage,
            }.DisposeList();
        }

        public IBlockStorage BlockStorage
        {
            get { return this.blockStorage; }
        }

        public IBlockTxesStorage BlockTxesStorage
        {
            get { return this.blockTxesStorage; }
        }

        public IChainStateCursor OpenChainStateCursor()
        {
            return this.chainStateManager.CreateOrLoadChainState();
        }
    }
}
