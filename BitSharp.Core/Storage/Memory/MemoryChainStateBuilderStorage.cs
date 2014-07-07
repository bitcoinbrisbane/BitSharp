using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryChainStateBuilderStorage : IChainStateBuilderStorage
    {
        private int blockHeight;
        private UInt256 blockHash;
        private ImmutableSortedDictionary<UInt256, UnspentTx>.Builder unspentTransactions;

        private int? savedBlockHeight;
        private UInt256? savedBlockHash;
        private ImmutableSortedDictionary<UInt256, UnspentTx> savedUnspentTransactions;

        public MemoryChainStateBuilderStorage(IChainStateStorage parentUtxo)
        {
            this.blockHeight = parentUtxo.BlockHeight;
            this.blockHash = parentUtxo.BlockHash;
            if (parentUtxo is MemoryChainStateStorage)
            {
                this.unspentTransactions = ((MemoryChainStateStorage)parentUtxo).UnspentTransactions.ToBuilder();
            }
            else
            {
                this.unspentTransactions = ImmutableSortedDictionary.CreateRange(parentUtxo.UnspentTransactions()).ToBuilder();
            }
        }

        public ImmutableSortedDictionary<UInt256, UnspentTx>.Builder UnspentTransactionsDictionary { get { return this.unspentTransactions; } }

        public int BlockHeight
        {
            get { return this.blockHeight; }
            set { this.blockHeight = value; }
        }

        public UInt256 BlockHash
        {
            get { return this.blockHash; }
            set { this.blockHash = value; }
        }

        public int TransactionCount
        {
            get { return this.unspentTransactions.Count; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return this.unspentTransactions.ContainsKey(txHash);
        }

        public bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx)
        {
            return this.unspentTransactions.TryGetValue(txHash, out unspentTx);
        }

        public bool TryGetTransaction(UInt256 txHash, int spentBlockIndex, out UnspentTx unspentTx)
        {
            throw new NotImplementedException();
        }

        public bool TryAddTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            try
            {
                this.unspentTransactions.Add(txHash, unspentTx);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public bool RemoveTransaction(UInt256 txHash, int spentBlockIndex)
        {
            return this.unspentTransactions.Remove(txHash);
        }

        public void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            this.unspentTransactions[txHash] = unspentTx;
        }

        IEnumerable<KeyValuePair<UInt256, UnspentTx>> IChainStateBuilderStorage.UnspentTransactions()
        {
            return this.unspentTransactions;
        }

        public void Flush()
        {
        }

        public IChainStateStorage ToImmutable()
        {
            //TODO figure out if creating clean dictionaries actually has any benefits
            if (true)
            {
                return new MemoryChainStateStorage(this.blockHeight, this.blockHash, this.unspentTransactions.ToImmutable());
            }
            else
            {
                var compactUnspentTransactions = ImmutableSortedDictionary.CreateBuilder<UInt256, UnspentTx>();
                foreach (var unspentTransaction in this.unspentTransactions)
                    compactUnspentTransactions.Add(unspentTransaction);

                return new MemoryChainStateStorage(this.blockHeight, this.blockHash, compactUnspentTransactions.ToImmutable());
            }
        }

        public void Dispose()
        {
        }

        public void BeginTransaction()
        {
            if (this.savedUnspentTransactions != null || this.savedBlockHash != null)
                throw new InvalidOperationException();

            //if (this.blockHeight % 10000 == 0)
            //{
            //    var compactUnspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();
            //    compactUnspentTransactions.AddRange(this.unspentTransactions);

            //    this.unspentTransactions = compactUnspentTransactions;
            //}

            this.savedUnspentTransactions = this.unspentTransactions.ToImmutable();
            this.savedBlockHeight = this.blockHeight;
            this.savedBlockHash = this.blockHash;
        }

        public void CommitTransaction()
        {
            if (this.savedUnspentTransactions == null || this.savedBlockHash == null)
                throw new InvalidOperationException();

            this.savedUnspentTransactions = null;
            this.savedBlockHeight = null;
            this.savedBlockHash = null;
        }

        public void RollbackTransaction()
        {
            if (this.savedUnspentTransactions == null || this.savedBlockHash == null)
                throw new InvalidOperationException();

            this.unspentTransactions = this.savedUnspentTransactions.ToBuilder();
            this.blockHeight = this.savedBlockHeight.Value;
            this.blockHash = this.savedBlockHash.Value;

            this.savedUnspentTransactions = null;
            this.savedBlockHeight = null;
            this.savedBlockHash = null;
        }
    }
}
