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

        private readonly object blockStorageLock;
        private readonly object blockTxesStorageLock;
        private readonly object chainStateManagerLock;

        private BlockStorage blockStorage;
        private BlockTxesStorage blockTxesStorage;
        private EsentChainStateManager chainStateManager;

        public EsentStorageManager(string baseDirectory, Logger logger)
        {
            this.logger = logger;
            this.baseDirectory = baseDirectory;

            this.blockStorageLock = new object();
            this.blockTxesStorageLock = new object();
            this.chainStateManagerLock = new object();
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
            get
            {
                if (this.blockStorage == null)
                    lock (this.blockStorageLock)
                        if (this.blockStorage == null)
                            this.blockStorage = new BlockStorage(this.baseDirectory, this.logger);

                return this.blockStorage;
            }
        }

        public IBlockTxesStorage BlockTxesStorage
        {
            get
            {
                if (this.blockTxesStorage == null)
                    lock (this.blockTxesStorageLock)
                        if (this.blockTxesStorage == null)
                            this.blockTxesStorage = new BlockTxesStorage(this.baseDirectory, this.logger);

                return this.blockTxesStorage;
            }
        }

        public IChainStateCursor OpenChainStateCursor()
        {
            if (this.chainStateManager == null)
                lock (this.chainStateManagerLock)
                    if (this.chainStateManager == null)
                        this.chainStateManager = new EsentChainStateManager(this.baseDirectory, this.logger);

            return this.chainStateManager.OpenChainStateCursor();
        }
    }
}
