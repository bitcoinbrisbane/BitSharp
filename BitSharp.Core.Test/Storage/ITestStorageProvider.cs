
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test.Storage
{
    public interface ITestStorageProvider
    {
        string Name { get; }

        void TestInitialize();

        void TestCleanup();

        IStorageManager OpenStorageManager(Logger logger);

        IChainStateBuilderStorage OpenChainStateBuilderStorage(ChainedHeader genesisHeader, Logger logger);
    }
}
