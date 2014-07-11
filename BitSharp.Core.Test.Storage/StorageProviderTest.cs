using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Test;
using BitSharp.Core.Test.Rules;
using BitSharp.Domain;
using BitSharp.Esent.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test.Storage
{
    [TestClass]
    public class StorageProviderTest
    {
        private readonly List<ITestStorageProvider> testStorageProviders =
            new List<ITestStorageProvider>
            {
                new MemoryTestStorageProvider(),
                new EsentTestStorageProvider(),
            };

        // Run the specified test method against all providers
        protected void RunTest(Action<ITestStorageProvider> testMethod)
        {
            foreach (var provider in testStorageProviders)
            {
                Debug.WriteLine("Testing provider: {0}".Format2(provider.Name));

                provider.TestInitialize();
                try
                {
                    testMethod(provider);
                }
                finally
                {
                    provider.TestCleanup();
                }
            }
        }
    }
}
