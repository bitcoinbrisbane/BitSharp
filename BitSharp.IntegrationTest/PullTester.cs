using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Ninject;
using NLog;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Storage.Memory;
using BitSharp.Node.Storage.Memory;
using BitSharp.Node.Storage;
using BitSharp.Core.Rules;
using BitSharp.Core;
using BitSharp.Node;
using System.Reflection;
using System.Threading;

namespace BitSharp.IntegrationTest
{
    [TestClass]
    public class PullTester
    {
        [TestMethod]
        public void TestPullTester()
        {
            // locate java.exe
            var javaPath = Path.Combine(Environment.GetEnvironmentVariable("JAVA_HOME"), "bin", "java.exe");
            if (!File.Exists(javaPath))
                Assert.Inconclusive("java.exe could not be found under JAVA_HOME");

            // clear comparison tool database files
            foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "BitcoindComparisonTool.*"))
                File.Delete(file);
            
            // initialize kernel
            using (var kernel = new StandardKernel())
            {
                // add logging module
                kernel.Load(new ConsoleLoggingModule(LogLevel.Info));

                // log startup
                var logger = kernel.Get<Logger>();
                logger.Info("Starting up: {0}".Format2(DateTime.Now));

                // add storage module
                kernel.Load(new MemoryStorageModule());
                kernel.Load(new NodeMemoryStorageModule());

                // add cache modules
                kernel.Load(new NodeCacheModule());

                // add rules module
                var rulesType = RulesEnum.ComparisonToolTestNet;
                kernel.Load(new RulesModule(rulesType));

                // initialize the blockchain daemon
                using (var coreDaemon = kernel.Get<CoreDaemon>())
                {
                    kernel.Bind<CoreDaemon>().ToConstant(coreDaemon).InTransientScope();

                    // initialize p2p client
                    using (var localClient = kernel.Get<LocalClient>())
                    {
                        kernel.Bind<LocalClient>().ToConstant(localClient).InTransientScope();

                        // start the blockchain daemon
                        coreDaemon.Start();

                        // start p2p client
                        localClient.Start();

                        // run pull tester
                        var javaProcessInfo = new ProcessStartInfo
                        {
                            FileName = javaPath,
                            Arguments = "-jar pull-tests.jar",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using (var javaProcess = Process.Start(javaProcessInfo))
                        {
                            javaProcess.OutputDataReceived += (sender, e) =>
                                logger.Info("[Pull Tester]:  " + e.Data);
                            javaProcess.ErrorDataReceived += (sender, e) =>
                                logger.Error("[Pull Tester]: " + e.Data);

                            javaProcess.BeginOutputReadLine();
                            javaProcess.BeginErrorReadLine();

                            javaProcess.WaitForExit();

                            logger.Info("Pull Tester Result: {0}".Format2(javaProcess.ExitCode));

                            Assert.Inconclusive("TODO");
                            Assert.AreEqual(0, javaProcess.ExitCode);
                        }
                    }
                }
            }
        }
    }
}
