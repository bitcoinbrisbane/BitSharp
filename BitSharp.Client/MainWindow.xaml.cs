//#define TEST_TOOL

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

namespace BitSharp.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IStorageContext storageContext;
        private ICacheContext cacheContext;
        private IBlockchainRules rules;
        private BlockchainDaemon blockchainDaemon;
        private LocalClient localClient;
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            try
            {
                //TODO
                //MainnetRules.BypassValidation = true;
                MainnetRules.BypassExecuteScript = true;
                ScriptEngine.BypassVerifySignature = true;

                Debug.WriteLine(DateTime.Now);

#if !TEST_TOOL
                var storageContext = new EsentStorageContext(Path.Combine(Config.LocalStoragePath, "data"), cacheSizeMaxBytes: 500.MILLION());
#else
                //if (Directory.Exists(Path.Combine(Config.LocalStoragePath, "data-test")))
                //    Directory.Delete(Path.Combine(Config.LocalStoragePath, "data-test"), recursive: true);
                //var storageContext = new EsentStorageContext(Path.Combine(Config.LocalStoragePath, "data-test"));
                var storageContext = new MemoryStorageContext();
#endif
                
                var knownAddressStorage = new BitSharp.Storage.Esent.KnownAddressStorage(storageContext);
                this.storageContext = storageContext;
                this.cacheContext = new CacheContext(this.storageContext);
                
#if !TEST_TOOL
                this.rules = new MainnetRules(this.cacheContext);
#else
                this.rules = new ComparisonToolTestNetRules(this.cacheContext);
#endif
                
                this.blockchainDaemon = new BlockchainDaemon(this.rules, this.cacheContext);
                
#if !TEST_TOOL
                this.localClient = new LocalClient(LocalClientType.MainNet, this.blockchainDaemon, knownAddressStorage);
#else
                this.localClient = new LocalClient(LocalClientType.ComparisonToolTestNet, this.blockchainDaemon, knownAddressStorage);
#endif

                // setup view model
                this.viewModel = new MainWindowViewModel(this.blockchainDaemon);

                InitializeComponent();

                // start the blockchain daemon
                this.blockchainDaemon.Start();

                this.viewModel.ViewBlockchainLast();

                // start p2p client
                this.localClient.Start();

                this.DataContext = this.viewModel;
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
            base.OnClosed(e);

            // shutdown
            new IDisposable[]
            {
                this.localClient,
                this.blockchainDaemon,
                this.storageContext
            }.DisposeList();
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
