using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.ExtensionMethods;
using BitSharp.Core.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Windows8;
using Microsoft.Isam.Esent.Interop.Windows81;
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
    internal class ChainStateCursor : IChainStateCursor
    {
        //TODO
        public static bool IndexOutputs { get; set; }

        private readonly Logger logger;

        private readonly string jetDatabase;
        private readonly Instance jetInstance;

        public readonly Session jetSession;
        public readonly JET_DBID chainStateDbId;

        public readonly JET_TABLEID globalsTableId;
        public readonly JET_COLUMNID unspentTxCountColumnId;

        public readonly JET_TABLEID chainTableId;
        public readonly JET_COLUMNID blockHeightColumnId;
        public readonly JET_COLUMNID chainedHeaderBytesColumnId;

        public readonly JET_TABLEID unspentTxTableId;
        public readonly JET_COLUMNID txHashColumnId;
        public readonly JET_COLUMNID blockIndexColumnId;
        public readonly JET_COLUMNID txIndexColumnId;
        public readonly JET_COLUMNID outputStatesColumnId;

        public readonly JET_TABLEID spentTxTableId;
        public readonly JET_COLUMNID spentSpentBlockIndexColumnId;
        public readonly JET_COLUMNID spentDataColumnId;

        private bool inTransaction;

        public ChainStateCursor(string jetDatabase, Instance jetInstance, Logger logger)
        {
            this.logger = logger;
            this.jetDatabase = jetDatabase;
            this.jetInstance = jetInstance;

            //TODO
            var readOnly = false;

            this.OpenCursor(this.jetDatabase, this.jetInstance, readOnly,
                out this.jetSession,
                out this.chainStateDbId,
                out this.globalsTableId,
                    out this.unspentTxCountColumnId,
                out this.chainTableId,
                    out this.blockHeightColumnId,
                    out this.chainedHeaderBytesColumnId,
                out this.unspentTxTableId,
                    out this.txHashColumnId,
                    out this.blockIndexColumnId,
                    out this.txIndexColumnId,
                    out this.outputStatesColumnId,
                out spentTxTableId,
                    out spentSpentBlockIndexColumnId,
                    out spentDataColumnId);
        }

        ~ChainStateCursor()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            Api.JetCloseDatabase(this.jetSession, this.chainStateDbId, CloseDatabaseGrbit.None);
            this.jetSession.Dispose();
        }

        public IEnumerable<ChainedHeader> ReadChain()
        {
            Api.JetSetCurrentIndex(this.jetSession, this.chainTableId, "IX_BlockHeight");

            if (Api.TryMoveFirst(this.jetSession, this.chainTableId))
            {
                do
                {
                    var chainedHeader = DataEncoder.DecodeChainedHeader(Api.RetrieveColumn(this.jetSession, this.chainTableId, this.chainedHeaderBytesColumnId));
                    yield return chainedHeader;
                } while (Api.TryMoveNext(this.jetSession, this.chainTableId));
            }
        }

        public void AddChainedHeader(ChainedHeader chainedHeader)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetPrepareUpdate(this.jetSession, this.chainTableId, JET_prep.Insert);
            try
            {
                Api.SetColumn(this.jetSession, this.chainTableId, this.blockHeightColumnId, chainedHeader.Height);
                Api.SetColumn(this.jetSession, this.chainTableId, this.chainedHeaderBytesColumnId, DataEncoder.EncodeChainedHeader(chainedHeader));

                Api.JetUpdate(this.jetSession, this.chainTableId);
            }
            catch (Exception e)
            {
                Api.JetPrepareUpdate(this.jetSession, this.chainTableId, JET_prep.Cancel);
                throw new InvalidOperationException("Failed to add chained header.", e);
            }
        }

        public void RemoveChainedHeader(ChainedHeader chainedHeader)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetSetCurrentIndex(this.jetSession, this.chainTableId, "IX_BlockHeight");

            Api.MakeKey(this.jetSession, this.chainTableId, chainedHeader.Height, MakeKeyGrbit.NewKey);

            if (!Api.TrySeek(this.jetSession, this.chainTableId, SeekGrbit.SeekEQ))
                throw new InvalidOperationException();

            Api.JetDelete(this.jetSession, this.chainTableId);
        }

        public int UnspentTxCount
        {
            get
            {
                return Api.RetrieveColumnAsInt32(this.jetSession, this.globalsTableId, this.unspentTxCountColumnId).Value;
            }
        }

        public bool ContainsUnspentTx(UInt256 txHash)
        {
            Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.jetSession, this.unspentTxTableId, DbEncoder.EncodeUInt256(txHash), MakeKeyGrbit.NewKey);
            return Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ);
        }

        public bool TryGetUnspentTx(UInt256 txHash, out UnspentTx unspentTx)
        {
            Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.jetSession, this.unspentTxTableId, DbEncoder.EncodeUInt256(txHash), MakeKeyGrbit.NewKey);
            if (Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
            {
                var blockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.blockIndexColumnId).Value;
                var txIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.txIndexColumnId).Value;
                var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));

                unspentTx = new UnspentTx(txHash, blockIndex, txIndex, outputStates);
                return true;
            }

            unspentTx = default(UnspentTx);
            return false;
        }

        public bool TryAddUnspentTx(UnspentTx unspentTx)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            try
            {
                Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Insert);
                try
                {
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.txHashColumnId, DbEncoder.EncodeUInt256(unspentTx.TxHash));
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.blockIndexColumnId, unspentTx.BlockIndex);
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.txIndexColumnId, unspentTx.TxIndex);
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId, DataEncoder.EncodeOutputStates(unspentTx.OutputStates));

                    Api.JetUpdate(this.jetSession, this.unspentTxTableId);

                    // increase unspent tx count
                    Api.EscrowUpdate(this.jetSession, this.globalsTableId, this.unspentTxCountColumnId, +1);

                    return true;
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Cancel);
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

            Api.JetPrepareUpdate(this.jetSession, this.spentTxTableId, JET_prep.Insert);
            try
            {
                Api.SetColumn(this.jetSession, this.spentTxTableId, this.spentSpentBlockIndexColumnId, spentBlockIndex);
                Api.SetColumn(this.jetSession, this.spentTxTableId, this.spentDataColumnId, new byte[0]);

                Api.JetUpdate(this.jetSession, this.spentTxTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(this.jetSession, this.spentTxTableId, JET_prep.Cancel);
                throw;
            }

            Api.JetSetCurrentIndex(this.jetSession, this.spentTxTableId, "IX_SpentBlockIndex");
            Api.MakeKey(this.jetSession, this.spentTxTableId, spentBlockIndex, MakeKeyGrbit.NewKey);
            if (!Api.TrySeek(this.jetSession, this.spentTxTableId, SeekGrbit.SeekEQ))
                throw new InvalidOperationException();
        }

        public bool TryRemoveUnspentTx(UInt256 txHash)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.jetSession, this.unspentTxTableId, DbEncoder.EncodeUInt256(txHash), MakeKeyGrbit.NewKey);
            if (Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
            {
                var addedBlockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.blockIndexColumnId).Value;
                var txIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.txIndexColumnId).Value;
                var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));

                Api.JetDelete(this.jetSession, this.unspentTxTableId);

                // decrease unspent tx count
                Api.EscrowUpdate(this.jetSession, this.globalsTableId, this.unspentTxCountColumnId, -1);

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryUpdateUnspentTx(UnspentTx unspentTx)
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.jetSession, this.unspentTxTableId, DbEncoder.EncodeUInt256(unspentTx.TxHash), MakeKeyGrbit.NewKey);

            if (Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
            {
                Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Replace);
                try
                {
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId, DataEncoder.EncodeOutputStates(unspentTx.OutputStates));

                    Api.JetUpdate(this.jetSession, this.unspentTxTableId);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Cancel);
                    throw;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public IEnumerable<UnspentTx> ReadUnspentTransactions()
        {
            Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");

            if (Api.TryMoveFirst(this.jetSession, this.unspentTxTableId))
            {
                do
                {
                    var txHash = DbEncoder.DecodeUInt256(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.txHashColumnId));
                    var blockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.blockIndexColumnId).Value;
                    var txIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.txIndexColumnId).Value;
                    var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));

                    yield return new UnspentTx(txHash, blockIndex, txIndex, outputStates);
                } while (Api.TryMoveNext(this.jetSession, this.unspentTxTableId));
            }
        }

        public IEnumerable<SpentTx> ReadSpentTransactions(int spentBlockIndex)
        {
            Api.JetSetCurrentIndex(this.jetSession, this.spentTxTableId, "IX_SpentBlockIndex");

            Api.MakeKey(this.jetSession, this.spentTxTableId, spentBlockIndex, MakeKeyGrbit.NewKey);

            if (Api.TrySeek(this.jetSession, this.spentTxTableId, SeekGrbit.SeekEQ))
            {
                var spentData = Api.RetrieveColumn(this.jetSession, this.spentTxTableId, this.spentDataColumnId);
                using (var stream = new MemoryStream(spentData))
                {
                    while (stream.Position < stream.Length)
                    {
                        yield return DataEncoder.DecodeSpentTx(stream);
                    }
                }
            }
        }

        public void AddSpentTransaction(SpentTx spentTx)
        {
            Debug.Assert(spentTx.SpentBlockIndex == Api.RetrieveColumnAsInt32(this.jetSession, this.spentTxTableId, this.spentSpentBlockIndexColumnId).Value);

            Api.JetPrepareUpdate(this.jetSession, this.spentTxTableId, JET_prep.Replace);
            try
            {
                var spentTxBytes = DataEncoder.EncodeSpentTx(spentTx);

                Api.SetColumn(this.jetSession, this.spentTxTableId, this.spentDataColumnId, spentTxBytes, SetColumnGrbit.AppendLV);

                Api.JetUpdate(this.jetSession, this.spentTxTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(this.jetSession, this.spentTxTableId, JET_prep.Cancel);
                throw;
            }
        }

        public void RemoveSpentTransactions(int spentBlockIndex)
        {
            Api.JetSetCurrentIndex(this.jetSession, this.spentTxTableId, "IX_SpentBlockIndex");

            Api.MakeKey(this.jetSession, this.spentTxTableId, spentBlockIndex, MakeKeyGrbit.NewKey);

            if (Api.TrySeek(this.jetSession, this.spentTxTableId, SeekGrbit.SeekEQ))
            {
                Api.JetDelete(this.jetSession, this.spentTxTableId);
            }
        }

        public void RemoveSpentTransactionsToHeight(int spentBlockIndex)
        {
            Api.JetSetCurrentIndex(this.jetSession, this.spentTxTableId, "IX_SpentBlockIndex");

            Api.MakeKey(this.jetSession, this.spentTxTableId, -1, MakeKeyGrbit.NewKey);

            if (Api.TrySeek(this.jetSession, this.spentTxTableId, SeekGrbit.SeekGE))
            {
                do
                {
                    if (spentBlockIndex >= Api.RetrieveColumnAsInt32(this.jetSession, this.spentTxTableId, this.spentSpentBlockIndexColumnId).Value)
                    {
                        Api.JetDelete(this.jetSession, this.spentTxTableId);
                    }
                    else
                    {
                        break;
                    }
                } while (Api.TryMoveNext(this.jetSession, this.spentTxTableId));
            }
        }

        public void BeginTransaction()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            Api.JetBeginTransaction(this.jetSession);

            this.inTransaction = true;
        }

        public void CommitTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);

            this.inTransaction = false;
        }

        public void RollbackTransaction()
        {
            if (!this.inTransaction)
                throw new InvalidOperationException();

            Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);

            this.inTransaction = false;
        }

        public void Defragment()
        {
            //int passes = -1, seconds = -1;
            //Api.JetDefragment(defragCursor.jetSession, defragCursor.chainStateDbId, "Chain", ref passes, ref seconds, DefragGrbit.BatchStart);
            //Api.JetDefragment(defragCursor.jetSession, defragCursor.chainStateDbId, "ChainState", ref passes, ref seconds, DefragGrbit.BatchStart);

            if (EsentVersion.SupportsWindows81Features)
            {
                this.logger.Info("Begin shrinking chain state database");

                int actualPages;
                Windows8Api.JetResizeDatabase(this.jetSession, this.chainStateDbId, 0, out actualPages, Windows81Grbits.OnlyShrink);

                this.logger.Info("Finished shrinking chain state database: {0:#,##0} pages".Format2(actualPages));
            }
        }

        private void OpenCursor(string jetDatabase, Instance jetInstance, bool readOnly,
            out Session jetSession,
            out JET_DBID chainStateDbId,
            out JET_TABLEID globalsTableId,
            out JET_COLUMNID unspentTxCountColumnId,
            out JET_TABLEID chainTableId,
            out JET_COLUMNID blockHeightColumnId,
            out JET_COLUMNID chainedHeaderBytesColumnId,
            out JET_TABLEID unspentTxTableId,
            out JET_COLUMNID txHashColumnId,
            out JET_COLUMNID blockIndexColumnId,
            out JET_COLUMNID txIndexColumnId,
            out JET_COLUMNID outputStatesColumnId,
            out JET_TABLEID spentTxTableId,
            out JET_COLUMNID spentSpentBlockIndexColumnId,
            out JET_COLUMNID spentDataColumnId)
        {
            jetSession = new Session(jetInstance);
            try
            {
                Api.JetOpenDatabase(jetSession, jetDatabase, "", out chainStateDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);

                Api.JetOpenTable(jetSession, chainStateDbId, "Globals", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out globalsTableId);
                unspentTxCountColumnId = Api.GetTableColumnid(jetSession, globalsTableId, "UnspentTxCount");

                if (!Api.TryMoveFirst(jetSession, globalsTableId))
                    throw new InvalidOperationException();

                Api.JetOpenTable(jetSession, chainStateDbId, "Chain", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out chainTableId);
                blockHeightColumnId = Api.GetTableColumnid(jetSession, chainTableId, "BlockHeight");
                chainedHeaderBytesColumnId = Api.GetTableColumnid(jetSession, chainTableId, "ChainedHeaderBytes");

                Api.JetOpenTable(jetSession, chainStateDbId, "UnspentTx", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out unspentTxTableId);
                txHashColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "TxHash");
                blockIndexColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "BlockIndex");
                txIndexColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "TxIndex");
                outputStatesColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "OutputStates");

                Api.JetOpenTable(jetSession, chainStateDbId, "SpentTx", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out spentTxTableId);
                spentSpentBlockIndexColumnId = Api.GetTableColumnid(jetSession, spentTxTableId, "SpentBlockIndex");
                spentDataColumnId = Api.GetTableColumnid(jetSession, spentTxTableId, "SpentData");
            }
            catch (Exception)
            {
                jetSession.Dispose();
                throw;
            }
        }
    }
}
