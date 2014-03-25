using BitSharp.Blockchain;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using BitSharp.Data;
using BitSharp.Storage;
using BitSharp.Storage.ExtensionMethods;
using BitSharp.Network;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BitSharp.Client
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly BlockchainDaemon blockchainDaemon;

        private ChainState viewChainState;

        private long _winningBlockchainHeight;
        private long _currentBlockchainHeight;
        private long _downloadedBlockCount;

        public MainWindowViewModel(BlockchainDaemon blockchainDaemon)
        {
            this.blockchainDaemon = blockchainDaemon;

            this.viewChainState = this.blockchainDaemon.ChainState;

            this.WinningBlockchainHeight = this.blockchainDaemon.TargetBlockHeight;
            this.CurrentBlockchainHeight = this.blockchainDaemon.CurrentBuilderHeight;
            this.DownloadedBlockCount = this.blockchainDaemon.CacheContext.BlockTxHashesCache.Count;

            this.blockchainDaemon.CacheContext.BlockTxHashesCache.OnAddition +=
                (blockHash, block) =>
                    DownloadedBlockCount = this.blockchainDaemon.CacheContext.BlockTxHashesCache.Count;

            this.blockchainDaemon.OnTargetBlockChanged +=
                (sender, block) =>
                    WinningBlockchainHeight = this.blockchainDaemon.TargetBlockHeight;

            this.blockchainDaemon.OnChainStateChanged +=
                (sender, chainState) =>
                    CurrentBlockchainHeight = this.blockchainDaemon.CurrentBuilderHeight;

            this.blockchainDaemon.OnChainStateBuilderChanged +=
                (sender, height) =>
                    CurrentBlockchainHeight = this.blockchainDaemon.CurrentBuilderHeight;
        }

        public long WinningBlockchainHeight
        {
            get { return this._winningBlockchainHeight; }
            set { SetValue(ref this._winningBlockchainHeight, value); }
        }

        public long CurrentBlockchainHeight
        {
            get { return this._currentBlockchainHeight; }
            set { SetValue(ref this._currentBlockchainHeight, value); }
        }

        public long DownloadedBlockCount
        {
            get { return this._downloadedBlockCount; }
            set { SetValue(ref this._downloadedBlockCount, value); }
        }

        public long ViewBlockchainHeight
        {
            get
            {
                return this.viewChainState.Height;
            }
            set
            {
                var chainStateLocal = this.blockchainDaemon.ChainState;

                var height = value;
                if (height > chainStateLocal.Height)
                    height = chainStateLocal.Height;

                var targetBlock = chainStateLocal.LastBlock;
                SetViewBlockchain(targetBlock);
            }
        }

        public IList<TxOutputKey> ViewBlockchainSpendOutputs { get; protected set; }

        public IList<TxOutputKey> ViewBlockchainReceiveOutputs { get; protected set; }

        public void ViewBlockchainFirst()
        {
            var chainStateLocal = this.blockchainDaemon.ChainState;
            if (chainStateLocal.Chain.Blocks.Count == 0)
                return;

            var targetBlock = chainStateLocal.Chain.Blocks.First();
            SetViewBlockchain(targetBlock);
        }

        public void ViewBlockchainPrevious()
        {
            var chainStateLocal = this.blockchainDaemon.ChainState;
            if (chainStateLocal.Chain.Blocks.Count == 0)
                return;

            var height = this.viewChainState.Height - 1;
            if (height > chainStateLocal.Height)
                height = chainStateLocal.Height;

            var targetBlock = chainStateLocal.LastBlock;
            SetViewBlockchain(targetBlock);
        }

        public void ViewBlockchainNext()
        {
            var chainStateLocal = this.blockchainDaemon.ChainState;
            if (chainStateLocal.Chain.Blocks.Count == 0)
                return;

            var height = this.viewChainState.Height + 1;
            if (height > chainStateLocal.Height)
                height = chainStateLocal.Height;

            var targetBlock = chainStateLocal.LastBlock;
            SetViewBlockchain(targetBlock);
        }

        public void ViewBlockchainLast()
        {
            SetViewBlockchain(this.blockchainDaemon.ChainState);
        }

        private void SetViewBlockchain(ChainedBlock targetBlock)
        {
            using (var cancelToken = new CancellationTokenSource())
            {
                try
                {
                    //TODO
                    //List<MissingDataException> missingData;
                    //var blockchain = this.blockchainDaemon.Calculator.CalculateBlockchainFromExisting(this.viewBlockchain, targetBlock, null, out missingData, cancelToken.Token);
                    //SetViewBlockchain(blockchain);
                }
                catch (MissingDataException)
                {
                    // TODO
                }
                catch (AggregateException e)
                {
                    if (!e.IsMissingDataOnly())
                        throw;
                }
            }
        }

        private void SetViewBlockchain(ChainState chainState)
        {
            this.viewChainState = chainState;

            try
            {
                if (chainState.Height > 0)
                {
                    var block = this.blockchainDaemon.CacheContext.BlockView[this.viewChainState.LastBlockHash];
                    // TODO this is abusing rollback a bit just to get the transactions that exist in a target block that's already known
                    // TODO make a better api for get the net output of a block
                    //List<TxOutputKey> spendOutputs, receiveOutputs;
                    //TODO
                    //this.blockchainDaemon.Calculator.RollbackBlockchain(this.viewBlockchain, block, out spendOutputs, out receiveOutputs);

                    //TODO
                    //ViewBlockchainSpendOutputs = spendOutputs;
                    //ViewBlockchainReceiveOutputs = receiveOutputs;
                }
                else
                {
                    ViewBlockchainSpendOutputs = new List<TxOutputKey>();
                    ViewBlockchainReceiveOutputs = new List<TxOutputKey>();
                }
            }
            catch (MissingDataException)
            {
                // TODO
            }
            catch (AggregateException e)
            {
                if (!e.IsMissingDataOnly())
                    throw;
            }

            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs("ViewBlockchainHeight"));
                handler(this, new PropertyChangedEventArgs("ViewBlockchainSpendOutputs"));
                handler(this, new PropertyChangedEventArgs("ViewBlockchainReceiveOutputs"));
            }
        }

        private void SetValue<T>(ref T currentValue, T newValue, [CallerMemberName] string propertyName = "") where T : IEquatable<T>
        {
            if (!currentValue.Equals(newValue))
            {
                currentValue = newValue;

                var handler = this.PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
