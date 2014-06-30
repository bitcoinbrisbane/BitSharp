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
        private ImmutableDictionary<UInt256, UnspentTx>.Builder unspentTransactions;
        //private ImmutableDictionary<TxOutputKey, TxOutput>.Builder unspentOutputs;

        private int? savedBlockHeight;
        private UInt256? savedBlockHash;
        private ImmutableDictionary<UInt256, UnspentTx> savedUnspentTransactions;
        //private ImmutableDictionary<TxOutputKey, TxOutput> savedUnspentOutputs;

        public MemoryChainStateBuilderStorage(IChainStateStorage parentUtxo)
        {
            this.blockHeight = parentUtxo.BlockHeight;
            this.blockHash = parentUtxo.BlockHash;
            if (parentUtxo is MemoryChainStateStorage)
            {
                this.unspentTransactions = ((MemoryChainStateStorage)parentUtxo).UnspentTransactions.ToBuilder();
                //this.unspentOutputs = ((MemoryChainStateStorage)parentUtxo).UnspentOutputs.ToBuilder();
            }
            else
            {
                this.unspentTransactions = ImmutableDictionary.CreateRange(parentUtxo.UnspentTransactions()).ToBuilder();
                //this.unspentOutputs = ImmutableDictionary.CreateRange(parentUtxo.UnspentOutputs()).ToBuilder();
            }
        }

        public ImmutableDictionary<UInt256, UnspentTx>.Builder UnspentTransactionsDictionary { get { return this.unspentTransactions; } }

        //public ImmutableDictionary<TxOutputKey, TxOutput>.Builder UnspentOutputsDictionary { get { return this.unspentOutputs; } }

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

        public void AddTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            this.unspentTransactions.Add(txHash, unspentTx);
        }

        public bool RemoveTransaction(UInt256 txHash)
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

        //public int OutputCount
        //{
        //    get { return this.unspentOutputs.Count; }
        //}

        //public bool ContainsOutput(TxOutputKey txOutputKey)
        //{
        //    return this.unspentOutputs.ContainsKey(txOutputKey);
        //}

        //public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        //{
        //    return this.unspentOutputs.TryGetValue(txOutputKey, out txOutput);
        //}

        //public void AddOutput(TxOutputKey txOutputKey, TxOutput txOutput)
        //{
        //    this.unspentOutputs.Add(txOutputKey, txOutput);
        //}

        //public bool RemoveOutput(TxOutputKey txOutputKey)
        //{
        //    return this.unspentOutputs.Remove(txOutputKey);
        //}

        //IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> IChainStateBuilderStorage.UnspentOutputs()
        //{
        //    return this.unspentOutputs;
        //}

        public void Flush()
        {
        }

        public IChainStateStorage ToImmutable()
        {
            //TODO figure out if creating clean dictionaries actually has any benefits
            if (true)
            {
                return new MemoryChainStateStorage(this.blockHeight, this.blockHash, this.unspentTransactions.ToImmutable()); //, this.unspentOutputs.ToImmutable());
            }
            else
            {
                var compactUnspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();
                foreach (var unspentTransaction in this.unspentTransactions)
                    compactUnspentTransactions.Add(unspentTransaction);

                //var compactUnspentOutputs = ImmutableDictionary.CreateBuilder<TxOutputKey, TxOutput>();
                //foreach (var unspentOutput in this.unspentOutputs)
                //    compactUnspentOutputs.Add(unspentOutput);

                return new MemoryChainStateStorage(this.blockHeight, this.blockHash, compactUnspentTransactions.ToImmutable()); //, compactUnspentOutputs.ToImmutable());
            }
        }

        public void Dispose()
        {
        }

        public void BeginTransaction()
        {
            if (this.savedUnspentTransactions != null || /*this.savedUnspentOutputs != null ||*/ this.savedBlockHash != null)
                throw new InvalidOperationException();

            //if (this.blockHeight % 10000 == 0)
            //{
            //    var compactUnspentTransactions = ImmutableDictionary.CreateBuilder<UInt256, UnspentTx>();
            //    compactUnspentTransactions.AddRange(this.unspentTransactions);

            //    this.unspentTransactions = compactUnspentTransactions;
            //}

            this.savedUnspentTransactions = this.unspentTransactions.ToImmutable();
            //this.savedUnspentOutputs = this.unspentOutputs.ToImmutable();
            this.savedBlockHeight = this.blockHeight;
            this.savedBlockHash = this.blockHash;
        }

        public void CommitTransaction()
        {
            if (this.savedUnspentTransactions == null || /*this.savedUnspentOutputs == null ||*/ this.savedBlockHash == null)
                throw new InvalidOperationException();

            this.savedUnspentTransactions = null;
            //this.savedUnspentOutputs = null;
            this.savedBlockHeight = null;
            this.savedBlockHash = null;
        }

        public void RollbackTransaction()
        {
            if (this.savedUnspentTransactions == null || /*this.savedUnspentOutputs == null ||*/ this.savedBlockHash == null)
                throw new InvalidOperationException();

            this.unspentTransactions = this.savedUnspentTransactions.ToBuilder();
            //this.unspentOutputs = this.savedUnspentOutputs.ToBuilder();
            this.blockHeight = this.savedBlockHeight.Value;
            this.blockHash = this.savedBlockHash.Value;

            this.savedUnspentTransactions = null;
            //this.savedUnspentOutputs = null;
            this.savedBlockHeight = null;
            this.savedBlockHash = null;
        }
    }
}
