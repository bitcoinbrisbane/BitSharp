using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Node.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Windows81;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    internal class EsentChainStateManager : IDisposable
    {
        private readonly Logger logger;
        private readonly string baseDirectory;
        private readonly string jetDirectory;
        private readonly string jetDatabase;

        private Instance jetInstance;
        private IChainStateBuilderStorage chainStateBuilderStorage;

        private readonly object chainStateBuilderStorageLock;

        public EsentChainStateManager(string baseDirectory, Logger logger)
        {
            this.logger = logger;
            this.baseDirectory = baseDirectory;
            this.jetDirectory = Path.Combine(baseDirectory, "ChainState");
            this.jetDatabase = Path.Combine(this.jetDirectory, "ChainState.edb");

            this.chainStateBuilderStorageLock = new object();
        }

        public void Dispose()
        {
            new IDisposable[] {
                this.jetInstance
            }.DisposeList();
        }

        public IChainStateBuilderStorage CreateOrLoadChainState(ChainedHeader genesisHeader)
        {
            lock (this.chainStateBuilderStorageLock)
            {
                if (this.chainStateBuilderStorage != null)
                    throw new InvalidOperationException();

                this.jetInstance = CreateInstance(this.jetDirectory);
                this.jetInstance.Init();

                this.CreateOrOpenDatabase(this.jetDirectory, this.jetDatabase, this.jetInstance, genesisHeader);

                this.chainStateBuilderStorage = new ChainStateBuilderStorage(this.jetDatabase, this.jetInstance, this.logger);

                return this.chainStateBuilderStorage;
            }
        }

        private void CreateOrOpenDatabase(string jetDirectory, string jetDatabase, Instance jetInstance, ChainedHeader genesisHeader)
        {
            try
            {
                this.OpenDatabase(jetDatabase, jetInstance);
            }
            catch (Exception)
            {
                try { Directory.Delete(jetDirectory, recursive: true); }
                catch (Exception) { }
                Directory.CreateDirectory(jetDirectory);

                CreateDatabase(jetDatabase, jetInstance, genesisHeader);
            }
        }

        private void CreateDatabase(string jetDatabase, Instance jetInstance, ChainedHeader genesisHeader)
        {
            JET_DBID utxoDbId;
            JET_TABLEID chainTableId;
            JET_COLUMNID blockHeightColumnId;
            JET_COLUMNID chainedHeaderBytesColumnId;
            JET_TABLEID unspentTxTableId;
            JET_COLUMNID txHashColumnId;
            JET_COLUMNID blockIndexColumnId;
            JET_COLUMNID txIndexColumnId;
            JET_COLUMNID outputStatesColumnId;
            JET_TABLEID spentTxTableId;
            JET_COLUMNID spentSpentBlockIndexColumnId;
            JET_COLUMNID spentDataColumnId;

            using (var jetSession = new Session(jetInstance))
            {
                Api.JetCreateDatabase(jetSession, jetDatabase, "", out utxoDbId, CreateDatabaseGrbit.None);

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

                // insert genesis chain header
                Api.JetPrepareUpdate(jetSession, chainTableId, JET_prep.Insert);
                try
                {
                    Api.SetColumn(jetSession, chainTableId, blockHeightColumnId, 0);
                    Api.SetColumn(jetSession, chainTableId, chainedHeaderBytesColumnId, DataEncoder.EncodeChainedHeader(genesisHeader));

                    Api.JetUpdate(jetSession, chainTableId);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(jetSession, chainTableId, JET_prep.Cancel);
                }

                Api.JetCloseTable(jetSession, chainTableId);

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
        }

        private void OpenDatabase(string jetDatabase, Instance jetInstance)
        {
            var readOnly = false;

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

        private static Instance CreateInstance(string directory)
        {
            var instance = new Instance(Guid.NewGuid().ToString());

            instance.Parameters.SystemDirectory = directory;
            instance.Parameters.LogFileDirectory = directory;
            instance.Parameters.TempDirectory = directory;
            instance.Parameters.AlternateDatabaseRecoveryDirectory = directory;
            instance.Parameters.CreatePathIfNotExist = true;
            instance.Parameters.BaseName = "epc";
            instance.Parameters.EnableIndexChecking = false;
            instance.Parameters.CircularLog = true;
            instance.Parameters.CheckpointDepthMax = 64 * 1024 * 1024;
            instance.Parameters.LogFileSize = 1024 * 32;
            instance.Parameters.LogBuffers = 1024 * 32;
            instance.Parameters.MaxTemporaryTables = 1;
            instance.Parameters.MaxVerPages = 1024 * 256;
            instance.Parameters.NoInformationEvent = true;
            instance.Parameters.WaypointLatency = 1;
            instance.Parameters.MaxSessions = 256;
            instance.Parameters.MaxOpenTables = 256;
            if (EsentVersion.SupportsWindows81Features)
            {
                instance.Parameters.EnableShrinkDatabase = ShrinkDatabaseGrbit.On | ShrinkDatabaseGrbit.Realtime;
            }

            return instance;
        }
    }
}
