using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.ExtensionMethods;
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

        public void PrepareSpentTransactions(int spentBlockIndex)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.spentTxTableId, JET_prep.Insert);
            try
            {
                Api.SetColumn(this.cursor.jetSession, this.cursor.spentTxTableId, this.cursor.spentSpentBlockIndexColumnId, spentBlockIndex);
                Api.SetColumn(this.cursor.jetSession, this.cursor.spentTxTableId, this.cursor.spentDataColumnId, new byte[0]);

                Api.JetUpdate(this.cursor.jetSession, this.cursor.spentTxTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.spentTxTableId, JET_prep.Cancel);
                throw;
            }

            Api.JetSetCurrentIndex(this.cursor.jetSession, this.cursor.spentTxTableId, "IX_SpentBlockIndex");
            Api.MakeKey(this.cursor.jetSession, this.cursor.spentTxTableId, spentBlockIndex, MakeKeyGrbit.NewKey);
            if (!Api.TrySeek(this.cursor.jetSession, this.cursor.spentTxTableId, SeekGrbit.SeekEQ))
                throw new InvalidOperationException();
        }

        public bool RemoveTransaction(UInt256 txHash, int spentBlockIndex)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetSetCurrentIndex(this.cursor.jetSession, this.cursor.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.cursor.jetSession, this.cursor.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
            if (!Api.TrySeek(this.cursor.jetSession, this.cursor.unspentTxTableId, SeekGrbit.SeekEQ))
                throw new KeyNotFoundException();

            var addedBlockIndex = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.unspentTxTableId, cursor.blockIndexColumnId).Value;
            var txIndex = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.unspentTxTableId, cursor.txIndexColumnId).Value;
            var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(cursor.jetSession, cursor.unspentTxTableId, cursor.outputStatesColumnId));

            Api.JetDelete(this.cursor.jetSession, this.cursor.unspentTxTableId);

            if (spentBlockIndex >= 0)
            {
                Debug.Assert(spentBlockIndex == Api.RetrieveColumnAsInt32(this.cursor.jetSession, this.cursor.spentTxTableId, this.cursor.spentSpentBlockIndexColumnId).Value);

                Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.spentTxTableId, JET_prep.Replace);
                try
                {
                    var spentTx = new SpentTx(txHash, addedBlockIndex, txIndex, outputStates.Length, spentBlockIndex);
                    var spentTxBytes = DataEncoder.EncodeSpentTx(spentTx);

                    Api.SetColumn(this.cursor.jetSession, this.cursor.spentTxTableId, this.cursor.spentDataColumnId, spentTxBytes, SetColumnGrbit.AppendLV);

                    Api.JetUpdate(this.cursor.jetSession, this.cursor.spentTxTableId);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.spentTxTableId, JET_prep.Cancel);
                    throw;
                }
            }

            return true;
        }

        public void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetSetCurrentIndex(this.cursor.jetSession, this.cursor.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.cursor.jetSession, this.cursor.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
            if (!Api.TrySeek(this.cursor.jetSession, this.cursor.unspentTxTableId, SeekGrbit.SeekEQ))
                throw new KeyNotFoundException();

            Api.JetPrepareUpdate(this.cursor.jetSession, this.cursor.unspentTxTableId, JET_prep.Replace);
            try
            {
                Api.SetColumn(this.cursor.jetSession, this.cursor.unspentTxTableId, this.cursor.outputStatesColumnId, DataEncoder.EncodeOutputStates(unspentTx.OutputStates));

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

        public IEnumerable<Tuple<int, int>> ReadSpentTransactions(int spentBlockIndex)
        {
            using (var readCursor = new ChainStateStorageCursor(this.jetDatabase, this.jetInstance, readOnly: true))
            {
                Api.JetSetCurrentIndex(readCursor.jetSession, readCursor.spentTxTableId, "IX_SpentBlockIndex");

                Api.MakeKey(readCursor.jetSession, readCursor.spentTxTableId, spentBlockIndex, MakeKeyGrbit.NewKey);

                if (Api.TrySeek(readCursor.jetSession, readCursor.spentTxTableId, SeekGrbit.SeekEQ))
                {
                    var spentData = Api.RetrieveColumn(readCursor.jetSession, readCursor.spentTxTableId, readCursor.spentDataColumnId);
                    using (var stream = new MemoryStream(spentData))
                    {
                        while (stream.Position < stream.Length)
                        {
                            var spentTx = DataEncoder.DecodeSpentTx(stream);
                            yield return Tuple.Create(spentTx.ConfirmedBlockIndex, spentTx.TxIndex);
                        }
                    }
                }
            }
        }

        public void RemoveSpentTransactions(int spentBlockIndex)
        {
            using (var pruneCursor = new ChainStateStorageCursor(this.jetDatabase, this.jetInstance, readOnly: false))
            {
                Api.JetBeginTransaction(pruneCursor.jetSession);
                try
                {
                    Api.JetSetCurrentIndex(pruneCursor.jetSession, pruneCursor.spentTxTableId, "IX_SpentBlockIndex");

                    Api.MakeKey(pruneCursor.jetSession, pruneCursor.spentTxTableId, spentBlockIndex, MakeKeyGrbit.NewKey);

                    if (Api.TrySeek(pruneCursor.jetSession, pruneCursor.spentTxTableId, SeekGrbit.SeekEQ))
                    {
                        Api.JetDelete(pruneCursor.jetSession, pruneCursor.spentTxTableId);
                    }

                    Api.JetCommitTransaction(pruneCursor.jetSession, CommitTransactionGrbit.LazyFlush);
                }
                catch (Exception)
                {
                    Api.JetRollback(pruneCursor.jetSession, RollbackTransactionGrbit.None);
                    throw;
                }
            }
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            using (var pruneCursor = new ChainStateStorageCursor(this.jetDatabase, this.jetInstance, readOnly: false))
            {
                Api.JetBeginTransaction(pruneCursor.jetSession);
                try
                {
                    Api.JetSetCurrentIndex(pruneCursor.jetSession, pruneCursor.spentTxTableId, "IX_SpentBlockIndex");

                    Api.MakeKey(pruneCursor.jetSession, pruneCursor.spentTxTableId, -1, MakeKeyGrbit.NewKey);

                    if (Api.TrySeek(pruneCursor.jetSession, pruneCursor.spentTxTableId, SeekGrbit.SeekGE))
                    {
                        do
                        {
                            if (spentBlockIndex >= Api.RetrieveColumnAsInt32(pruneCursor.jetSession, pruneCursor.spentTxTableId, pruneCursor.spentSpentBlockIndexColumnId).Value)
                            {
                                Api.JetDelete(pruneCursor.jetSession, pruneCursor.spentTxTableId);
                            }
                            else
                            {
                                break;
                            }
                        } while (Api.TryMove(pruneCursor.jetSession, pruneCursor.spentTxTableId, JET_Move.Next, MoveGrbit.None));
                    }

                    Api.JetCommitTransaction(pruneCursor.jetSession, CommitTransactionGrbit.LazyFlush);
                }
                catch (Exception)
                {
                    Api.JetRollback(pruneCursor.jetSession, RollbackTransactionGrbit.None);
                    throw;
                }
            }
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
