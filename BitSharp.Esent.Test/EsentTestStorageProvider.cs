using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Core.Test.Storage;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent.Test
{
    public class EsentTestStorageProvider : ITestStorageProvider
    {
        private string baseDirectory;
        private EsentChainStateManager chainStateManager;

        public string Name { get { return "Esent Storage"; } }

        public void TestInitialize()
        {
            this.baseDirectory = Path.Combine(Path.GetTempPath(), "BitSharp", "Tests");

            if (Directory.Exists(this.baseDirectory))
                Directory.Delete(this.baseDirectory, recursive: true);

            Directory.CreateDirectory(this.baseDirectory);
        }

        public void TestCleanup()
        {
            if (this.chainStateManager != null)
            {
                this.chainStateManager.Dispose();
                this.chainStateManager = null;
            }

            if (Directory.Exists(this.baseDirectory))
                Directory.Delete(this.baseDirectory, recursive: true);
        }

        public IStorageManager OpenStorageManager(Logger logger)
        {
            return new EsentStorageManager(this.baseDirectory, logger);
        }

        public IChainStateBuilderStorage OpenChainStateBuilderStorage(ChainedHeader genesisHeader, Logger logger)
        {
            this.chainStateManager = new EsentChainStateManager(this.baseDirectory, logger);
            return this.chainStateManager.CreateOrLoadChainState(genesisHeader);
        }
    }
}
