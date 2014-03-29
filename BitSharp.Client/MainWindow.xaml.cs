//#define TEST_TOOL
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
using BitSharp.Storage.MongoDB;
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

#if TEST_TOOL
                modules.Add(new MemoryStorageModule());
#elif MEMORY
                modules.Add(new MemoryStorageModule());
#elif MONGODB
                modules.Add(new MongoStorageModule());
#elif MIXED
                modules.Add(new MixedStorageModule(Path.Combine(Config.LocalStoragePath, "data"), cacheSizeMaxBytes: 500.MILLION()));
#else
                modules.Add(new EsentStorageModule(Path.Combine(Config.LocalStoragePath, "data"), cacheSizeMaxBytes: 500.MILLION()));
#endif

#if TEST_TOOL
                modules.Add(new RulesModule(RulesEnum.ComparisonToolTestNet));
                this.rules = new Testnet2Rules(this.cacheContext);
#else
                modules.Add(new RulesModule(RulesEnum.MainNet));
#endif

                kernel.Bind<BlockchainDaemon>().ToSelf().InSingletonScope();
                kernel.Bind<LocalClient>().ToSelf().InSingletonScope();

                // setup view model
                this.viewModel = new MainWindowViewModel(kernel);

                InitializeComponent();

                // start the blockchain daemon
                var blockchainDaemon = kernel.Get<BlockchainDaemon>();
                blockchainDaemon.Start();

                this.viewModel.ViewBlockchainLast();

                // start p2p client
                var localClient = kernel.Get<LocalClient>();
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
                throw;
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
