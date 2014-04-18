//#define TEST_TOOL
//#define TESTNET3
//#define MEMORY
//#define TRANSIENT_BLOCKS
#define DUMMY_MONITOR

using BitSharp.Common.ExtensionMethods;
using BitSharp.Node;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Ninject;
using Ninject.Modules;
using NLog;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core;
using BitSharp.Esent;
using BitSharp.Node.Storage;
using BitSharp.Core.Wallet;
using BitSharp.Core.Wallet.Address;

namespace BitSharp.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IKernel kernel;
        private Logger logger;
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            try
            {
                //TODO
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "Data", "ChainState");
                try { Directory.Delete(path, recursive: true); }
                catch (Exception) { }
                Directory.CreateDirectory(path);

                //TODO
                //MainnetRules.BypassValidation = true;
                MainnetRules.IgnoreScriptErrors = true;

                // initialize kernel
                this.kernel = new StandardKernel();

                // add logging module
                this.kernel.Load(new LoggingModule(LogLevel.Info));

                // log startup
                this.logger = kernel.Get<Logger>();
                this.logger.Info("Starting up: {0}".Format2(DateTime.Now));

                var modules = new List<INinjectModule>();

                // add storage module
#if TEST_TOOL
                modules.Add(new MemoryStorageModule());
#elif MEMORY
                modules.Add(new MemoryStorageModule());
#elif TRANSIENT_BLOCKS
                modules.Add(new EsentStorageModule(Path.Combine(Config.LocalStoragePath, "Data"), transientBlockStorage: true));
                //ChainStateBuilderStorage.IndexOutputs = true;
#else
                modules.Add(new EsentStorageModule(Path.Combine(Config.LocalStoragePath, "Data"), cacheSizeMaxBytes: int.MaxValue - 1));
                //ChainStateBuilderStorage.IndexOutputs = true;
#endif

                // add cache modules
                modules.Add(new CoreCacheModule());
                modules.Add(new NodeCacheModule());

                // add rules module
#if TEST_TOOL
                modules.Add(new RulesModule(RulesEnum.ComparisonToolTestNet));
#elif TESTNET3
                modules.Add(new RulesModule(RulesEnum.TestNet3));
#else
                modules.Add(new RulesModule(RulesEnum.MainNet));
#endif

                // load modules
                this.kernel.Load(modules.ToArray());

                // initialize the blockchain daemon
                this.kernel.Bind<CoreDaemon>().ToSelf().InSingletonScope();
                var blockchainDaemon = this.kernel.Get<CoreDaemon>();

#if DUMMY_MONITOR
                blockchainDaemon.SubscribeChainStateVisitor(new DummyMonitor(this.logger));
#endif

                // setup view model
                this.viewModel = new MainWindowViewModel(this.kernel);
                InitializeComponent();
                this.viewModel.ViewBlockchainLast();

                // start the blockchain daemon
                blockchainDaemon.Start();

                // start p2p client
                this.kernel.Bind<LocalClient>().ToSelf().InSingletonScope();
                var localClient = this.kernel.Get<LocalClient>();
                var startThread = new Thread(() => localClient.Start());
                startThread.Name = "LocalClient.Start";
                startThread.Start();

                this.DataContext = this.viewModel;

#if TEST_TOOL
                var bitcoinjThread = new Thread(
                    ()=>
                    {
                        var projectFolder = Environment.CurrentDirectory;
                        while (projectFolder.Contains(@"\BitSharp.Client"))
                            projectFolder = Path.GetDirectoryName(projectFolder);

                        File.Delete(Path.Combine(projectFolder, "Bitcoinj-comparison.log"));

                        var javaProcessStartInfo = new ProcessStartInfo
                            {
                                FileName = @"C:\Program Files\Java\jdk1.7.0_25\bin\java.exe",
                                WorkingDirectory = projectFolder,
                                Arguments = @"-Djava.util.logging.config.file={0}\bitcoinj.log.properties -jar {0}\bitcoinj.jar".Format2(projectFolder),
                                UseShellExecute = false
                            };

                        var javaProcess = Process.Start(javaProcessStartInfo);
                    });
                bitcoinjThread.Start();
#endif
            }
            catch (Exception e)
            {
                if (this.logger != null)
                {
                    this.logger.FatalException("Application failed", e);
                    LogManager.Flush();
                }
                else
                {
                    Console.WriteLine(e);
                }

                Environment.Exit(-1);
            }
        }

        public MainWindowViewModel ViewModel { get { return this.viewModel; } }

        protected override void OnClosed(EventArgs e)
        {
            // shutdown
            this.kernel.Dispose();

            base.OnClosed(e);
        }

        private void ViewFirst_Click(object sender, RoutedEventArgs e)
        {
            this.viewModel.ViewBlockchainFirst();
        }

        private void ViewPrevious_Click(object sender, RoutedEventArgs e)
        {
            this.viewModel.ViewBlockchainPrevious();
        }

        private void ViewNext_Click(object sender, RoutedEventArgs e)
        {
            this.viewModel.ViewBlockchainNext();
        }

        private void ViewLast_Click(object sender, RoutedEventArgs e)
        {
            this.viewModel.ViewBlockchainLast();
        }

#if DUMMY_MONITOR
        private sealed class DummyMonitor : WalletMonitor
        {
            public DummyMonitor(Logger logger)
                : base(logger)
            {
                this.AddAddress(new First10000Address());
                this.AddAddress(new Top10000Address());
            }
        }
#endif
    }
}
