using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    internal class ChainStateBuilderStorage : IChainStateBuilderStorage
    {
        //TODO
        public static bool IndexOutputs { get; set; }

        private readonly Logger logger;

        private readonly string jetDatabase;
        private readonly Instance jetInstance;

        private readonly ChainStateStorageCursor cursor;

        private ChainBuilder chain;
        private Chain savedChain;

        private bool inTransaction;

        public ChainStateBuilderStorage(string jetDatabase, Instance jetInstance, Logger logger)
        {
            this.logger = logger;
            this.jetDatabase = jetDatabase;
            this.jetInstance = jetInstance;

            this.cursor = new ChainStateStorageCursor(jetDatabase, jetInstance, readOnly: false);

            this.chain = ChainStateStorage.ReadChain(this.cursor).ToBuilder();
        }

        ~ChainStateBuilderStorage()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            this.cursor.Dispose();
        }

        public Chain Chain
        {
            get { return this.chain.ToImmutable(); }
        }

        public void AddChainedHeader(ChainedHeader chainedHeader)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            var savedChain = this.chain.ToImmutable();
            this.chain.AddBlock(chainedHeader);
            try
            {
                Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.chainTableId, JET_prep.Insert);
                try
                {
                    Api.SetColumn(this.cursor.jetSession, this.cursor.chainTableId, this.cursor.blockHeightColumnId, chainedHeader.Height);
                    Api.SetColumn(this.cursor.jetSession, this.cursor.chainTableId, this.cursor.chainedHeaderBytesColumnId, DataEncoder.EncodeChainedHeader(chainedHeader));

                    Api.JetUpdate(this.cursor.jetSession, this.cursor.chainTableId);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId, JET_prep.Cancel);
                    throw;
                }
            }
            catch (Exception)
            {
                this.chain = savedChain.ToBuilder();
                throw;
            }
        }

        public void RemoveChainedHeader(ChainedHeader chainedHeader)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            var savedChain = this.chain.ToImmutable();
            this.chain.RemoveBlock(chainedHeader);
            try
            {
                Api.JetSetCurrentIndex(this.cursor.jetSession, this.cursor.chainTableId, "IX_BlockHeight");

                Api.MakeKey(this.cursor.jetSession, this.cursor.chainTableId, chainedHeader.Height, MakeKeyGrbit.NewKey);

                if (!Api.TrySeek(this.cursor.jetSession, this.cursor.chainTableId, SeekGrbit.SeekEQ))
                    throw new InvalidOperationException();

                Api.JetDelete(this.cursor.jetSession, this.cursor.chainTableId);
            }
            catch (Exception)
            {
                this.chain = savedChain.ToBuilder();
                throw;
            }
        }

        public int TransactionCount
        {
            //TODO
            get { return 0; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return ChainStateStorage.ContainsTransaction(this.cursor, txHash);
        }

        public bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx)
        {
            return ChainStateStorage.TryGetTransaction(this.cursor, txHash, out unspentTx);
        }

        public bool TryGetTransaction(UInt256 txHash, int spentBlockIndex, out UnspentTx unspentTx)
        {
            return ChainStateStorage.TryGetTransaction(this.cursor, txHash, spentBlockIndex, out unspentTx);
        }

        public bool TryAddTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            try
            {
                Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId, JET_prep.Insert);
                try
                {
                    Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.txHashColumnId, txHash.ToByteArray());
                    Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.blockIndexColumnId, unspentTx.BlockIndex);
                    Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.txIndexColumnId, unspentTx.TxIndex);
                    Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.outputStatesColumnId, DataEncoder.EncodeOutputStates(unspentTx.OutputStates));
                    Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.spentBlockIndexColumnId, null);

                    Api.JetUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId);

                    return true;
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId, JET_prep.Cancel);
                    throw;
                }
            }
            catch (EsentKeyDuplicateException)
            {
                return false;
            }
        }

        public bool RemoveTransaction(UInt256 txHash, int spentBlockIndex)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.cursor.jetSession, this.cursor.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
            if (!Api.TrySeek(this.cursor.jetSession, this.cursor.unspentTxTableId, SeekGrbit.SeekEQ))
                throw new KeyNotFoundException();

            if (spentBlockIndex < 0)
            {
                Api.JetDelete(this.cursor.jetSession, this.cursor.unspentTxTableId);
            }
            else
            {
                Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId, JET_prep.Replace);
                try
                {
                    Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.spentBlockIndexColumnId, spentBlockIndex);

                    Api.JetUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId, JET_prep.Cancel);
                    throw;
                }
            }

            return true;
        }

        public void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.cursor.jetSession, this.cursor.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
            if (!Api.TrySeek(this.cursor.jetSession, this.cursor.unspentTxTableId, SeekGrbit.SeekEQ))
                throw new KeyNotFoundException();

            Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId, JET_prep.Replace);
            try
            {
                Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.outputStatesColumnId, DataEncoder.EncodeOutputStates(unspentTx.OutputStates));
                Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.spentBlockIndexColumnId, null);

                Api.JetUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId, JET_prep.Cancel);
                throw;
            }
        }

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> ReadUnspentTransactions()
        {
            return ChainStateStorage.ReadUnspentTransactions(this.cursor);
        }

        public IChainStateStorage ToImmutable()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            return new ChainStateStorage(this.jetDatabase, this.jetInstance, this.chain.ToImmutable());
        }

        public void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            Api.JetBeginTransaction(this.cursor.jetSession);
            this.savedChain = this.chain.ToImmutable();

            this.inTransaction = true;
        }

        public void CommitTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetCommitTransaction(this.cursor.jetSession, CommitTransactionGrbit.LazyFlush);
            this.savedChain = null;

            this.inTransaction = false;
        }

        public void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetRollback(this.cursor.jetSession, RollbackTransactionGrbit.None);
            this.chain = this.savedChain.ToBuilder();

            this.inTransaction = false;
        }
    }
}
