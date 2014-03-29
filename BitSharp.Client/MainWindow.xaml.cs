//#define TEST_TOOL
//#define TESTNET3
//#define MEMORY
//#define MONGODB
#define MIXED

using BitSharp.Common.ExtensionMethods;
using BitSharp.Blockchain;
using BitSharp.Daemon;
using BitSharp.Node;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.Storage.Esent;
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
using BitSharp.Network;
using Ninject;
using Ninject.Modules;

namespace BitSharp.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IKernel kernel;
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            try
            {
                //TODO
                //MainnetRules.BypassValidation = true;
                MainnetRules.IgnoreScriptErrors = true;

                Debug.WriteLine(DateTime.Now);

                var modules = new List<INinjectModule>();

                // add storage module
#if TEST_TOOL
                modules.Add(new MemoryStorageModule());
#elif MEMORY
                modules.Add(new MemoryStorageModule());
#elif MONGODB
                modules.Add(new MongoStorageModule());
#elif MIXED
                modules.Add(new MixedStorageModule(Path.Combine(Config.LocalStoragePath, "data"), cacheSizeMaxBytes: int.MaxValue)); // 500.MILLION()));
#else
                modules.Add(new EsentStorageModule(Path.Combine(Config.LocalStoragePath, "data"), cacheSizeMaxBytes: 500.MILLION()));
#endif

                // add cache module
                modules.Add(new CacheModule());

                // add rules module
#if TEST_TOOL
                modules.Add(new RulesModule(RulesEnum.ComparisonToolTestNet));
#elif TESTNET3
                modules.Add(new RulesModule(RulesEnum.TestNet3));
#else
                modules.Add(new RulesModule(RulesEnum.MainNet));
#endif

                // initialize kernel
                this.kernel = new StandardKernel(modules.ToArray());

                // start the blockchain daemon
                this.kernel.Bind<BlockchainDaemon>().ToSelf().InSingletonScope();
                var blockchainDaemon = this.kernel.Get<BlockchainDaemon>();
                blockchainDaemon.Start();

                // setup view model
                this.viewModel = new MainWindowViewModel(this.kernel);
                InitializeComponent();
                this.viewModel.ViewBlockchainLast();

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
                Debug.WriteLine(e);
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
    }
}
