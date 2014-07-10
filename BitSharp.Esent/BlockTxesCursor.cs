using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    internal class BlockTxesCursor : IDisposable
    {
        public readonly string jetDatabase;
        public readonly Instance jetInstance;

        public readonly Session jetSession;
        public readonly JET_DBID blockDbId;

        public readonly JET_TABLEID globalTableId;
        public readonly JET_COLUMNID flushColumnId;

        public readonly JET_TABLEID blocksTableId;
        public readonly JET_COLUMNID blockHashColumnId;
        public readonly JET_COLUMNID blockTxIndexColumnId;
        public readonly JET_COLUMNID blockDepthColumnId;
        public readonly JET_COLUMNID blockTxHashColumnId;
        public readonly JET_COLUMNID blockTxBytesColumnId;

        public BlockTxesCursor(string jetDatabase, Instance jetInstance)
        {
            this.jetDatabase = jetDatabase;
            this.jetInstance = jetInstance;

            this.OpenCursor(this.jetDatabase, this.jetInstance, false /*readOnly*/,
                out this.jetSession,
                out this.blockDbId,
                out this.globalTableId,
                    out this.flushColumnId,
                out this.blocksTableId,
                    out this.blockHashColumnId,
                    out this.blockTxIndexColumnId,
                    out this.blockDepthColumnId,
                    out this.blockTxHashColumnId,
                    out this.blockTxBytesColumnId);
        }

        public void Dispose()
        {
            this.jetSession.Dispose();
        }

        private void OpenCursor(string jetDatabase, Instance jetInstance, bool readOnly,
            out Session jetSession,
            out JET_DBID blockDbId,
            out JET_TABLEID globalTableId,
            out JET_COLUMNID flushColumnId,
            out JET_TABLEID blocksTableId,
            out JET_COLUMNID blockHashColumnId,
            out JET_COLUMNID blockTxIndexColumnId,
            out JET_COLUMNID blockDepthColumnId,
            out JET_COLUMNID blockTxHashColumnId,
            out JET_COLUMNID blockTxBytesColumnId)
        {
            jetSession = new Session(jetInstance);
            try
            {
                Api.JetAttachDatabase(jetSession, jetDatabase, readOnly ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None);
                Api.JetOpenDatabase(jetSession, jetDatabase, "", out blockDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);

                Api.JetOpenTable(jetSession, blockDbId, "global", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out globalTableId);
                flushColumnId = Api.GetTableColumnid(jetSession, globalTableId, "Flush");

                Api.JetOpenTable(jetSession, blockDbId, "Blocks", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out blocksTableId);
                blockHashColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "BlockHash");
                blockTxIndexColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "TxIndex");
                blockDepthColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "Depth");
                blockTxHashColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "TxHash");
                blockTxBytesColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "TxBytes");
            }
            catch (Exception)
            {
                jetSession.Dispose();
                throw;
            }
        }
    }
}
