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

        public readonly JET_TABLEID globalsTableId;
        public readonly JET_COLUMNID blockCountColumnId;
        public readonly JET_COLUMNID flushColumnId;

        public readonly JET_TABLEID blockIdsTableId;
        public readonly JET_COLUMNID blockIdsHashColumnId;
        public readonly JET_COLUMNID blockIdsIdColumnId;

        public readonly JET_TABLEID blocksTableId;
        public readonly JET_COLUMNID blockIdColumnId;
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
                out this.globalsTableId,
                    out this.blockCountColumnId,
                    out this.flushColumnId,
                out this.blockIdsTableId,
                    out this.blockIdsHashColumnId,
                    out this.blockIdsIdColumnId,
                out this.blocksTableId,
                    out this.blockIdColumnId,
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
            out JET_TABLEID globalsTableId,
            out JET_COLUMNID blockCountColumnId,
            out JET_COLUMNID flushColumnId,
            out JET_TABLEID blockIdsTableId,
            out JET_COLUMNID blockIdsHashColumnId,
            out JET_COLUMNID blockIdsIdColumnId,
            out JET_TABLEID blocksTableId,
            out JET_COLUMNID blockIdColumnId,
            out JET_COLUMNID blockTxIndexColumnId,
            out JET_COLUMNID blockDepthColumnId,
            out JET_COLUMNID blockTxHashColumnId,
            out JET_COLUMNID blockTxBytesColumnId)
        {
            jetSession = new Session(jetInstance);
            try
            {
                Api.JetOpenDatabase(jetSession, jetDatabase, "", out blockDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);

                Api.JetOpenTable(jetSession, blockDbId, "Globals", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out globalsTableId);
                blockCountColumnId = Api.GetTableColumnid(jetSession, globalsTableId, "BlockCount");
                flushColumnId = Api.GetTableColumnid(jetSession, globalsTableId, "Flush");

                if (!Api.TryMoveFirst(jetSession, globalsTableId))
                    throw new InvalidOperationException();

                Api.JetOpenTable(jetSession, blockDbId, "BlockIds", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out blockIdsTableId);
                blockIdsHashColumnId = Api.GetTableColumnid(jetSession, blockIdsTableId, "BlockHash");
                blockIdsIdColumnId = Api.GetTableColumnid(jetSession, blockIdsTableId, "BlockId");

                Api.JetOpenTable(jetSession, blockDbId, "Blocks", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out blocksTableId);
                blockIdColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "BlockId");
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
