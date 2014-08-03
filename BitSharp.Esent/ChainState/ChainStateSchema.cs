using BitSharp.Core;
using BitSharp.Core.Domain;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent.ChainState
{
    internal static class ChainStateSchema
    {
        public static void CreateDatabase(string jetDatabase, Instance jetInstance)
        {
            JET_DBID utxoDbId;

            using (var jetSession = new Session(jetInstance))
            {
                Api.JetCreateDatabase(jetSession, jetDatabase, "", out utxoDbId, CreateDatabaseGrbit.None);

                CreateChainTable(utxoDbId, jetSession);
                CreateUnspentTxTable(utxoDbId, jetSession);
                CreateSpentTxTable(utxoDbId, jetSession);
            }
        }

        private static void CreateChainTable(JET_DBID utxoDbId, Session jetSession)
        {
            JET_TABLEID chainTableId;
            JET_COLUMNID blockHeightColumnId;
            JET_COLUMNID chainedHeaderBytesColumnId;

            Api.JetCreateTable(jetSession, utxoDbId, "Chain", 0, 0, out chainTableId);
            Api.JetAddColumn(jetSession, chainTableId, "BlockHeight", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out blockHeightColumnId);
            Api.JetAddColumn(jetSession, chainTableId, "ChainedHeaderBytes", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out chainedHeaderBytesColumnId);

            Api.JetCreateIndex2(jetSession, chainTableId,
                new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_BlockHeight",
                            szKey = "+BlockHeight\0\0",
                            cbKey = "+BlockHeight\0\0".Length
                        }
                    }, 1);

            Api.JetCloseTable(jetSession, chainTableId);
        }

        private static void CreateUnspentTxTable(JET_DBID utxoDbId, Session jetSession)
        {
            JET_TABLEID unspentTxTableId;
            JET_COLUMNID txHashColumnId;
            JET_COLUMNID blockIndexColumnId;
            JET_COLUMNID txIndexColumnId;
            JET_COLUMNID outputStatesColumnId;

            Api.JetCreateTable(jetSession, utxoDbId, "UnspentTx", 0, 0, out unspentTxTableId);
            Api.JetAddColumn(jetSession, unspentTxTableId, "TxHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out txHashColumnId);
            Api.JetAddColumn(jetSession, unspentTxTableId, "BlockIndex", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out blockIndexColumnId);
            Api.JetAddColumn(jetSession, unspentTxTableId, "TxIndex", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out txIndexColumnId);
            Api.JetAddColumn(jetSession, unspentTxTableId, "OutputStates", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out outputStatesColumnId);

            Api.JetCreateIndex2(jetSession, unspentTxTableId,
                new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexUnique | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_TxHash",
                            szKey = "+TxHash\0\0",
                            cbKey = "+TxHash\0\0".Length
                        }
                    }, 1);

            Api.JetCloseTable(jetSession, unspentTxTableId);
        }

        private static void CreateSpentTxTable(JET_DBID utxoDbId, Session jetSession)
        {
            JET_TABLEID spentTxTableId;
            JET_COLUMNID spentSpentBlockIndexColumnId;
            JET_COLUMNID spentDataColumnId;

            Api.JetCreateTable(jetSession, utxoDbId, "SpentTx", 0, 0, out spentTxTableId);
            Api.JetAddColumn(jetSession, spentTxTableId, "SpentBlockIndex", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out spentSpentBlockIndexColumnId);
            Api.JetAddColumn(jetSession, spentTxTableId, "SpentData", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out spentDataColumnId);

            Api.JetCreateIndex2(jetSession, spentTxTableId,
                new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_SpentBlockIndex",
                            szKey = "+SpentBlockIndex\0\0",
                            cbKey = "+SpentBlockIndex\0\0".Length
                        }
                    }, 1);

            Api.JetCloseTable(jetSession, spentTxTableId);
        }

        public static void OpenDatabase(string jetDatabase, Instance jetInstance, bool readOnly)
        {
            using (var jetSession = new Session(jetInstance))
            {
                Api.JetAttachDatabase(jetSession, jetDatabase, readOnly ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None);
                try
                {
                    var cursor = new ChainStateStorageCursor(jetDatabase, jetInstance, readOnly: true);
                    cursor.Dispose();
                }
                catch (Exception)
                {
                    Api.JetDetachDatabase(jetSession, jetDatabase);
                    throw;
                }
            }
        }
    }
}
