using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Wallet
{
    //TODO the wallet is currently intimately tied to the chain state
    //TODO what do i do if the chain state commits before the wallet has had a chance to save its current state?
    //TODO i could potentially use the saved blocks & stored rollback information to replay any blocks that the wallet is behind on
    //TODO this would be useful to have in general, as wallets could then be turned off & on and then catch up properly

    public class WalletMonitor : ChainStateVisitorBase
    {
        private readonly Logger logger;

        // addresses
        private readonly Dictionary<UInt256, List<MonitoredWalletAddress>> addressesByOutputScriptHash;
        private readonly List<MonitoredWalletAddress> matcherAddresses;

        // current point in the blockchain

        // entries
        private readonly ImmutableList<WalletEntry>.Builder entries;
        private readonly ReaderWriterLockSlim entriesLock;

        private decimal bitBalance;

        public WalletMonitor(Logger logger)
        {
            this.logger = logger;
            this.addressesByOutputScriptHash = new Dictionary<UInt256, List<MonitoredWalletAddress>>();
            this.matcherAddresses = new List<MonitoredWalletAddress>();
            this.entries = ImmutableList.CreateBuilder<WalletEntry>();
            this.entriesLock = new ReaderWriterLockSlim();
            this.bitBalance = 0;
        }

        public event Action<WalletEntry> OnEntryAdded;

        public IImmutableList<WalletEntry> Entries
        {
            get
            {
                return this.entriesLock.DoRead(() =>
                    this.entries.ToImmutable());
            }
        }

        public decimal BitBalance
        {
            get
            {
                return this.entriesLock.DoRead(() =>
                    this.bitBalance);
            }
        }

        //TODO thread safety
        //TODO need to rescan utxo when addresses are added as well
        public void AddAddress(IWalletAddress address)
        {
            //TODO add to queue, cannot monitor address until chain position moves
            var startChainPosition = ChainPosition.Fake();
            var monitoredRange = new[] { Tuple.Create(startChainPosition, startChainPosition) }.ToList();

            foreach (var outputScriptHash in address.GetOutputScriptHashes())
            {
                List<MonitoredWalletAddress> addresses;
                if (!this.addressesByOutputScriptHash.TryGetValue(outputScriptHash, out addresses))
                {
                    addresses = new List<MonitoredWalletAddress>();
                    this.addressesByOutputScriptHash.Add(outputScriptHash, addresses);
                }

                addresses.Add(new MonitoredWalletAddress(address, monitoredRange));
            }

            if (address.IsMatcher)
            {
                this.matcherAddresses.Add(new MonitoredWalletAddress(address, monitoredRange));
            }
        }

        public override void MintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase)
        {
            this.ScanForEntry(chainPosition, isCoinbase ? EnumWalletEntryType.Mine : EnumWalletEntryType.Receive, txOutput, outputScriptHash);
        }

        public override void SpendTxOutput(ChainPosition chainPosition, ChainedHeader chainedHeader, Transaction tx, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash)
        {
            this.ScanForEntry(chainPosition, EnumWalletEntryType.Spend, txOutput, outputScriptHash);
        }

        private void ScanForEntry(ChainPosition chainPosition, EnumWalletEntryType walletEntryType, TxOutput txOutput, UInt256 outputScriptHash)
        {
            var matchingAddresses = ImmutableList.CreateBuilder<MonitoredWalletAddress>();

            // test hash addresses
            List<MonitoredWalletAddress> addresses;
            if (this.addressesByOutputScriptHash.TryGetValue(outputScriptHash, out addresses))
            {
                matchingAddresses.AddRange(addresses);
            }

            // test matcher addresses
            foreach (var address in this.matcherAddresses)
            {
                if (address.Address.MatchesTxOutput(txOutput, outputScriptHash))
                    matchingAddresses.Add(address);
            }

            if (matchingAddresses.Count > 0)
            {
                var entry = new WalletEntry
                (
                    addresses: matchingAddresses.ToImmutable(),
                    type: walletEntryType,
                    chainPosition: chainPosition,
                    value: txOutput.Value
                );

                this.entriesLock.DoWrite(() =>
                {
                    this.logger.Debug("{0,-10}   {1,20:#,##0.000_000_00} BTC, Entries: {2:#,##0}".Format2(walletEntryType.ToString() + ":", txOutput.Value / (decimal)(100.MILLION()), this.entries.Count));

                    this.entries.Add(entry);
                    if (walletEntryType == EnumWalletEntryType.Spend)
                        this.bitBalance -= entry.BitValue;
                    else
                        this.bitBalance += entry.BitValue;
                });

                var handler = this.OnEntryAdded;
                if (handler != null)
                    handler(entry);
            }
        }
    }
}
