using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
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
    public class ChainStateBuilderStorage : IChainStateBuilderStorage
    {
        //TODO
        public static bool IndexOutputs { get; set; }

        private readonly Logger logger;

        private readonly bool isSnapshot;
        private readonly string jetDirectory;
        private readonly string jetDatabase;
        private readonly Instance jetInstance;
        private Session jetSession;
        private bool inTransaction;

        private readonly JET_DBID utxoDbId;

        private readonly JET_TABLEID globalTableId;
        private readonly JET_COLUMNID blockHeightColumnId;
        private readonly JET_COLUMNID blockHashColumnId;

        private readonly JET_TABLEID unspentTxTableId;
        private readonly JET_COLUMNID txHashColumnId;
        private readonly JET_COLUMNID blockIndexColumnId;
        private readonly JET_COLUMNID txIndexColumnId;
        private readonly JET_COLUMNID outputStatesColumnId;
        private readonly JET_COLUMNID spentBlockIndexColumnId;

        private int unspentTxCount;

        public ChainStateBuilderStorage(string baseDirectory, IChainStateStorage parentUtxo, Logger logger)
        {
            this.isSnapshot = false;
            this.logger = logger;
            this.jetDirectory = Path.Combine(baseDirectory, "ChainState");
            this.jetDatabase = Path.Combine(this.jetDirectory, "ChainState.edb");

            //TODO currently configured by PersistentDictionary
            //SystemParameters.DatabasePageSize = 8192;
            //var maxDbSizeInPages = 500.MILLION() / SystemParameters.DatabasePageSize;

            this.jetInstance = CreateInstance(this.jetDirectory);
            this.jetInstance.Init();

            CreateOrOpenDatabase(this.jetDirectory, this.jetDatabase, this.jetInstance, false /*readOnly*/,
                out this.jetSession,
                out this.utxoDbId,
                out this.globalTableId,
                    out this.blockHeightColumnId, out this.blockHashColumnId,
                out this.unspentTxTableId,
                    out this.txHashColumnId, out this.blockIndexColumnId, out this.txIndexColumnId, out this.outputStatesColumnId, out this.spentBlockIndexColumnId);

            this.BlockHeight = parentUtxo.BlockHeight;
            this.BlockHash = parentUtxo.BlockHash;

            foreach (var unspentTx in parentUtxo.UnspentTransactions())
                this.TryAddTransaction(unspentTx.Key, unspentTx.Value);
        }

        public ChainStateBuilderStorage(ChainStateBuilderStorage parentStorage)
        {
            this.isSnapshot = true;
            this.logger = parentStorage.logger;
            this.jetDirectory = parentStorage.jetDirectory;
            this.jetDatabase = parentStorage.jetDatabase;
            this.jetInstance = parentStorage.jetInstance;

            OpenDatabase(this.jetDatabase, this.jetInstance, true /*readOnly*/,
                out this.jetSession,
                out this.utxoDbId,
                out this.globalTableId,
                    out this.blockHeightColumnId, out this.blockHashColumnId,
                out this.unspentTxTableId,
                    out this.txHashColumnId, out this.blockIndexColumnId, out this.txIndexColumnId, out this.outputStatesColumnId, out this.spentBlockIndexColumnId);

            Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
        }

        ~ChainStateBuilderStorage()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            this.jetSession.Dispose();

            if (!this.isSnapshot)
                this.jetInstance.Dispose();
        }

        public int BlockHeight
        {
            get
            {
                Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
                try
                {
                    if (!Api.TryMoveFirst(this.jetSession, this.globalTableId))
                        throw new Exception();

                    return Api.RetrieveColumnAsInt32(this.jetSession, this.globalTableId, this.blockHeightColumnId).Value;
                }
                finally
                {
                    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                }
            }
            set
            {
                if (Api.TryMoveFirst(this.jetSession, this.globalTableId))
                    Api.JetPrepareUpdate(this.jetSession, this.globalTableId, JET_prep.Replace);
                else
                    Api.JetPrepareUpdate(this.jetSession, this.globalTableId, JET_prep.Insert);
                try
                {
                    Api.SetColumn(this.jetSession, this.globalTableId, this.blockHeightColumnId, value);
                    Api.JetUpdate(this.jetSession, this.globalTableId);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.jetSession, this.globalTableId, JET_prep.Cancel);
                    throw;
                }
            }
        }

        public UInt256 BlockHash
        {
            get
            {
                Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
                try
                {
                    if (!Api.TryMoveFirst(this.jetSession, this.globalTableId))
                        throw new Exception();

                    return new UInt256(Api.RetrieveColumn(this.jetSession, this.globalTableId, this.blockHashColumnId));
                }
                finally
                {
                    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                }
            }
            set
            {
                if (Api.TryMoveFirst(this.jetSession, this.globalTableId))
                    Api.JetPrepareUpdate(this.jetSession, this.globalTableId, JET_prep.Replace);
                else
                    Api.JetPrepareUpdate(this.jetSession, this.globalTableId, JET_prep.Insert);
                try
                {
                    Api.SetColumn(this.jetSession, this.globalTableId, this.blockHashColumnId, value.ToByteArray());
                    Api.JetUpdate(this.jetSession, this.globalTableId);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.jetSession, this.globalTableId, JET_prep.Cancel);
                    throw;
                }
            }
        }

        public int TransactionCount
        {
            get
            {
                return this.unspentTxCount;
            }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.jetSession, this.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
            return Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ);
        }

        public bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx)
        {
            Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
                Api.MakeKey(this.jetSession, this.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
                if (Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
                {
                    var blockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.blockIndexColumnId).Value;
                    var txIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.txIndexColumnId).Value;
                    var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));
                    var spentBlockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.spentBlockIndexColumnId);

                    if (spentBlockIndex == null)
                    {
                        unspentTx = new UnspentTx(blockIndex, txIndex, outputStates);
                        return true;
                    }
                }

                unspentTx = default(UnspentTx);
                return false;
            }
            finally
            {
                Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
            }
        }

        public bool TryGetTransaction(UInt256 txHash, int spentBlockIndex, out UnspentTx unspentTx)
        {
            Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
                Api.MakeKey(this.jetSession, this.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
                if (Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
                {
                    var blockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.blockIndexColumnId).Value;
                    var txIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.txIndexColumnId).Value;
                    var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));
                    var storedSpentBlockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.spentBlockIndexColumnId);

                    if (storedSpentBlockIndex == null || storedSpentBlockIndex.Value == spentBlockIndex)
                    {
                        unspentTx = new UnspentTx(blockIndex, txIndex, outputStates);
                        return true;
                    }
                }

                unspentTx = default(UnspentTx);
                return false;
            }
            finally
            {
                Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
            }
        }

        public bool TryAddTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            try
            {
                Api.JetBeginTransaction(this.jetSession);
                try
                {
                    Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Insert);
                    try
                    {
                        Api.SetColumn(this.jetSession, this.unspentTxTableId, this.txHashColumnId, txHash.ToByteArray());
                        Api.SetColumn(this.jetSession, this.unspentTxTableId, this.blockIndexColumnId, unspentTx.BlockIndex);
                        Api.SetColumn(this.jetSession, this.unspentTxTableId, this.txIndexColumnId, unspentTx.TxIndex);
                        Api.SetColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId, DataEncoder.EncodeOutputStates(unspentTx.OutputStates));
                        Api.SetColumn(this.jetSession, this.unspentTxTableId, this.spentBlockIndexColumnId, null);

                        Api.JetUpdate(this.jetSession, this.unspentTxTableId);
                        Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                        this.unspentTxCount++;

                        return true;
                    }
                    catch (Exception)
                    {
                        Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Cancel);
                        throw;
                    }
                }
                catch (Exception)
                {
                    Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);
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
            Api.JetBeginTransaction(this.jetSession);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
                Api.MakeKey(this.jetSession, this.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
                if (!Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
                    throw new KeyNotFoundException();

                if (false)
                {
                    Api.JetDelete(this.jetSession, this.unspentTxTableId);
                }
                else
                {
                    Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Replace);
                    try
                    {
                        Api.SetColumn(this.jetSession, this.unspentTxTableId, this.spentBlockIndexColumnId, spentBlockIndex);

                        Api.JetUpdate(this.jetSession, this.unspentTxTableId);
                    }
                    catch (Exception)
                    {
                        Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Cancel);
                        throw;
                    }
                }

                Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                this.unspentTxCount--;
                return true;
            }
            catch (Exception)
            {
                Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);
                throw;
            }
        }

        public void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            Api.JetBeginTransaction(this.jetSession);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
                Api.MakeKey(this.jetSession, this.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
                if (!Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
                    throw new KeyNotFoundException();

                Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Replace);
                try
                {
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId, DataEncoder.EncodeOutputStates(unspentTx.OutputStates));
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.spentBlockIndexColumnId, null);

                    Api.JetUpdate(this.jetSession, this.unspentTxTableId);
                    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Cancel);
                    throw;
                }
            }
            catch (Exception)
            {
                Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);
                throw;
            }
        }

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> UnspentTransactions()
        {
            Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
                Api.MoveBeforeFirst(this.jetSession, this.unspentTxTableId);
                while (Api.TryMoveNext(this.jetSession, this.unspentTxTableId))
                {
                    var txHash = new UInt256(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.txHashColumnId));
                    var blockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.blockIndexColumnId).Value;
                    var txIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.txIndexColumnId).Value;
                    var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));
                    var spentBlockIndex = Api.RetrieveColumnAsInt32(this.jetSession, this.unspentTxTableId, this.spentBlockIndexColumnId);

                    if (spentBlockIndex == null)
                    {
                        yield return new KeyValuePair<UInt256, UnspentTx>(txHash, new UnspentTx(blockIndex, txIndex, outputStates));
                    }
                }
            }
            finally
            {
                Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
            }
        }

        public void Flush()
        {
            throw new NotImplementedException();
        }

        public IChainStateStorage ToImmutable()
        {
            if (this.inTransaction)
                throw new InvalidOperationException();

            return new ChainStateStorage(this);
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
            instance.Parameters.MaxTemporaryTables = 0;
            instance.Parameters.MaxVerPages = 1024 * 256;
            instance.Parameters.NoInformationEvent = true;
            instance.Parameters.WaypointLatency = 1;
            instance.Parameters.MaxSessions = 256;
            instance.Parameters.MaxOpenTables = 256;

            return instance;
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

        private static void CreateOrOpenDatabase(string jetDirectory, string jetDatabase, Instance jetInstance, bool readOnly,
            out Session jetSession,
            out JET_DBID utxoDbId,
            out JET_TABLEID globalTableId,
            out JET_COLUMNID blockHeightColumnId,
            out JET_COLUMNID blockHashColumnId,
            out JET_TABLEID unspentTxTableId,
            out JET_COLUMNID txHashColumnId,
            out JET_COLUMNID blockIndexColumnId,
            out JET_COLUMNID txIndexColumnId,
            out JET_COLUMNID outputStatesColumnId,
            out JET_COLUMNID spentBlockIndexColumnId)
        {
            //TODO
            try { Directory.Delete(jetDirectory, recursive: true); }
            catch (Exception) { }
            Directory.CreateDirectory(jetDirectory);

            try
            {
                OpenDatabase(jetDatabase, jetInstance, readOnly, out jetSession, out utxoDbId, out globalTableId, out blockHeightColumnId, out blockHashColumnId, out unspentTxTableId, out txHashColumnId, out blockIndexColumnId, out txIndexColumnId, out outputStatesColumnId, out spentBlockIndexColumnId);
            }
            catch (Exception)
            {
                try { Directory.Delete(jetDirectory, recursive: true); }
                catch (Exception) { }
                Directory.CreateDirectory(jetDirectory);

                CreateDatabase(jetDatabase, jetInstance);
                OpenDatabase(jetDatabase, jetInstance, readOnly, out jetSession, out utxoDbId, out globalTableId, out blockHeightColumnId, out blockHashColumnId, out unspentTxTableId, out txHashColumnId, out blockIndexColumnId, out txIndexColumnId, out outputStatesColumnId, out spentBlockIndexColumnId);
            }
        }

        private static void CreateDatabase(string jetDatabase, Instance jetInstance)
        {
            JET_DBID utxoDbId;
            JET_TABLEID globalTableId;
            JET_COLUMNID blockHeightColumnId;
            JET_COLUMNID blockHashColumnId;
            JET_TABLEID unspentTxTableId;
            JET_COLUMNID txHashColumnId;
            JET_COLUMNID blockIndexColumnId;
            JET_COLUMNID txIndexColumnId;
            JET_COLUMNID outputStatesColumnId;
            JET_COLUMNID spentBlockIndexColumnId;

            using (var jetSession = new Session(jetInstance))
            {
                Api.JetCreateDatabase(jetSession, jetDatabase, "", out utxoDbId, CreateDatabaseGrbit.None);

                Api.JetCreateTable(jetSession, utxoDbId, "global", 0, 0, out globalTableId);
                Api.JetAddColumn(jetSession, globalTableId, "BlockHeight", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = /*TODO ColumndefGrbit.ColumnNotNULL |*/ ColumndefGrbit.ColumnFixed }, null, 0, out blockHeightColumnId);
                Api.JetAddColumn(jetSession, globalTableId, "BlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = /*TODO ColumndefGrbit.ColumnNotNULL |*/ ColumndefGrbit.ColumnFixed }, null, 0, out blockHashColumnId);

                Api.JetCloseTable(jetSession, globalTableId);

                Api.JetCreateTable(jetSession, utxoDbId, "unspentTx", 0, 0, out unspentTxTableId);
                Api.JetAddColumn(jetSession, unspentTxTableId, "TxHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out txHashColumnId);
                Api.JetAddColumn(jetSession, unspentTxTableId, "BlockIndex", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockIndexColumnId);
                Api.JetAddColumn(jetSession, unspentTxTableId, "TxIndex", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out txIndexColumnId);
                Api.JetAddColumn(jetSession, unspentTxTableId, "OutputStates", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out outputStatesColumnId);
                Api.JetAddColumn(jetSession, unspentTxTableId, "SpentBlockIndex", new JET_COLUMNDEF { coltyp = JET_coltyp.Long }, null, 0, out spentBlockIndexColumnId);

                Api.JetCreateIndex2(jetSession, unspentTxTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_TxHash",
                            szKey = "+TxHash\0\0",
                            cbKey = "+TxHash\0\0".Length
                        }
                    }, 1);

                Api.JetCreateIndex2(jetSession, unspentTxTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexIgnoreAnyNull,
                            szIndexName = "IX_SpentBlockIndex",
                            szKey = "+SpentBlockIndex\0\0",
                            cbKey = "+SpentBlockIndex\0\0".Length
                        }
                    }, 1);

                Api.JetCloseTable(jetSession, unspentTxTableId);
            }
        }

        private static void OpenDatabase(string jetDatabase, Instance jetInstance, bool readOnly,
            out Session jetSession,
            out JET_DBID utxoDbId,
            out JET_TABLEID globalTableId,
            out JET_COLUMNID blockHeightColumnId,
            out JET_COLUMNID blockHashColumnId,
            out JET_TABLEID unspentTxTableId,
            out JET_COLUMNID txHashColumnId,
            out JET_COLUMNID blockIndexColumnId,
            out JET_COLUMNID txIndexColumnId,
            out JET_COLUMNID outputStatesColumnId,
            out JET_COLUMNID spentBlockColumnId)
        {
            jetSession = new Session(jetInstance);
            try
            {
                Api.JetAttachDatabase(jetSession, jetDatabase, readOnly ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None);
                try
                {
                    Api.JetOpenDatabase(jetSession, jetDatabase, "", out utxoDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);
                    try
                    {
                        Api.JetOpenTable(jetSession, utxoDbId, "global", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out globalTableId);
                        blockHeightColumnId = Api.GetTableColumnid(jetSession, globalTableId, "BlockHeight");
                        blockHashColumnId = Api.GetTableColumnid(jetSession, globalTableId, "BlockHash");

                        Api.JetOpenTable(jetSession, utxoDbId, "unspentTx", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out unspentTxTableId);
                        txHashColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "TxHash");
                        blockIndexColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "BlockIndex");
                        txIndexColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "TxIndex");
                        outputStatesColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "OutputStates");
                        spentBlockColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "SpentBlockIndex");
                    }
                    catch (Exception)
                    {
                        Api.JetCloseDatabase(jetSession, utxoDbId, CloseDatabaseGrbit.None);
                        throw;
                    }
                }
                catch (Exception)
                {
                    Api.JetDetachDatabase(jetSession, jetDatabase);
                    throw;
                }
            }
            catch (Exception)
            {
                jetSession.Dispose();
                throw;
            }
        }
    }
}
