﻿using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    internal class ChainStateStorageCursor : IDisposable
    {
        public readonly string jetDatabase;
        public readonly Instance jetInstance;

        public readonly Session jetSession;
        public readonly JET_DBID chainStateDbId;

        public readonly JET_TABLEID chainTableId;
        public readonly JET_COLUMNID blockHeightColumnId;
        public readonly JET_COLUMNID chainedHeaderBytesColumnId;

        public readonly JET_TABLEID unspentTxTableId;
        public readonly JET_COLUMNID txHashColumnId;
        public readonly JET_COLUMNID blockIndexColumnId;
        public readonly JET_COLUMNID txIndexColumnId;
        public readonly JET_COLUMNID outputStatesColumnId;
        public readonly JET_COLUMNID spentBlockIndexColumnId;

        public ChainStateStorageCursor(string jetDatabase, Instance jetInstance, bool readOnly)
        {
            this.jetDatabase = jetDatabase;
            this.jetInstance = jetInstance;

            this.OpenCursor(this.jetDatabase, this.jetInstance, readOnly,
                out this.jetSession,
                out this.chainStateDbId,
                out this.chainTableId,
                    out this.blockHeightColumnId,
                    out this.chainedHeaderBytesColumnId,
                out this.unspentTxTableId,
                    out this.txHashColumnId,
                    out this.blockIndexColumnId,
                    out this.txIndexColumnId,
                    out this.outputStatesColumnId,
                    out this.spentBlockIndexColumnId);
        }

        public void Dispose()
        {
            this.jetSession.Dispose();
        }

        private void OpenCursor(string jetDatabase, Instance jetInstance, bool readOnly,
            out Session jetSession,
            out JET_DBID chainStateDbId,
            out JET_TABLEID chainTableId,
            out JET_COLUMNID blockHeightColumnId,
            out JET_COLUMNID chainedHeaderBytesColumnId,
            out JET_TABLEID unspentTxTableId,
            out JET_COLUMNID txHashColumnId,
            out JET_COLUMNID blockIndexColumnId,
            out JET_COLUMNID txIndexColumnId,
            out JET_COLUMNID outputStatesColumnId,
            out JET_COLUMNID spentBlockIndexColumnId)
        {
            jetSession = new Session(jetInstance);
            try
            {
                Api.JetAttachDatabase(jetSession, jetDatabase, readOnly ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None);
                Api.JetOpenDatabase(jetSession, jetDatabase, "", out chainStateDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);

                Api.JetOpenTable(jetSession, chainStateDbId, "Chain", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out chainTableId);
                blockHeightColumnId = Api.GetTableColumnid(jetSession, chainTableId, "BlockHeight");
                chainedHeaderBytesColumnId = Api.GetTableColumnid(jetSession, chainTableId, "ChainedHeaderBytes");

                Api.JetOpenTable(jetSession, chainStateDbId, "UnspentTx", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out unspentTxTableId);
                txHashColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "TxHash");
                blockIndexColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "BlockIndex");
                txIndexColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "TxIndex");
                outputStatesColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "OutputStates");
                spentBlockIndexColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "SpentBlockIndex");
            }
            catch (Exception)
            {
                jetSession.Dispose();
                throw;
            }
        }
    }
}
