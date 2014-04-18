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
        private readonly JET_COLUMNID blockHashColumnId;

        private readonly JET_TABLEID unspentTxTableId;
        private readonly JET_COLUMNID txHashColumnId;
        private readonly JET_COLUMNID confirmedBlockHashColumnId;
        private readonly JET_COLUMNID outputStatesColumnId;

        private readonly JET_TABLEID unspentTxOutputsTableId;
        private readonly JET_COLUMNID txOutputKeyColumnId;
        private readonly JET_COLUMNID txOutputColumnId;
        private readonly JET_COLUMNID outputScriptHashColumnId;

        private int unspentTxCount;
        private int unspentTxOutputsCount;

        public ChainStateBuilderStorage(string baseDirectory, IChainStateStorage parentUtxo, Logger logger)
        {
            this.isSnapshot = false;
            this.logger = logger;
            this.jetDirectory = Path.Combine(baseDirectory, "ChainState");
            this.jetDatabase = Path.Combine(this.jetDirectory, "ChainState.edb");

            //TODO currently configured by PersistentDictionary
            //SystemParameters.DatabasePageSize = 8192;
            //var maxDbSizeInPages = 500.MILLION() / SystemParameters.DatabasePageSize;

            this.jetInstance = CreateInstance2(this.jetDirectory);
            this.jetInstance.Init();

            CreateDatabase(this.jetDatabase, this.jetInstance);
            OpenDatabase(this.jetDatabase, this.jetInstance, false /*readOnly*/,
                out this.jetSession,
                out this.utxoDbId,
                out this.globalTableId,
                    out this.blockHashColumnId,
                out this.unspentTxTableId,
                    out this.txHashColumnId, out this.confirmedBlockHashColumnId, out this.outputStatesColumnId,
                out this.unspentTxOutputsTableId,
                    out this.txOutputKeyColumnId, out this.txOutputColumnId, out this.outputScriptHashColumnId);

            this.BlockHash = parentUtxo.BlockHash;

            foreach (var unspentTx in parentUtxo.UnspentTransactions())
                this.AddTransaction(unspentTx.Key, unspentTx.Value);

            foreach (var unspentOutput in parentUtxo.UnspentOutputs())
                this.AddOutput(unspentOutput.Key, unspentOutput.Value);
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
                    out this.blockHashColumnId,
                out this.unspentTxTableId,
                    out this.txHashColumnId, out this.confirmedBlockHashColumnId, out this.outputStatesColumnId,
                out this.unspentTxOutputsTableId,
                    out this.txOutputKeyColumnId, out this.txOutputColumnId, out this.outputScriptHashColumnId);

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
                    var confirmedBlockHash = new UInt256(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.confirmedBlockHashColumnId));
                    var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));

                    unspentTx = new UnspentTx(confirmedBlockHash, outputStates);
                    return true;
                }
                else
                {
                    unspentTx = default(UnspentTx);
                    return false;
                }
            }
            finally
            {
                Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
            }
        }

        public void AddTransaction(UInt256 txHash, UnspentTx unspentTx)
        {
            Api.JetBeginTransaction(this.jetSession);
            try
            {
                Api.JetPrepareUpdate(this.jetSession, this.unspentTxTableId, JET_prep.Insert);
                try
                {
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.txHashColumnId, txHash.ToByteArray());
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.confirmedBlockHashColumnId, unspentTx.ConfirmedBlockHash.ToByteArray());
                    Api.SetColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId, DataEncoder.EncodeOutputStates(unspentTx.OutputStates));

                    Api.JetUpdate(this.jetSession, this.unspentTxTableId);
                    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                    this.unspentTxCount++;
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

        public bool RemoveTransaction(UInt256 txHash)
        {
            Api.JetBeginTransaction(this.jetSession);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
                Api.MakeKey(this.jetSession, this.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
                if (!Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
                    throw new KeyNotFoundException();

                Api.JetDelete(this.jetSession, this.unspentTxTableId);

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
                    var confirmedBlockHash = new UInt256(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.confirmedBlockHashColumnId));
                    var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));

                    yield return new KeyValuePair<UInt256, UnspentTx>(txHash, new UnspentTx(confirmedBlockHash, outputStates));
                }
            }
            finally
            {
                Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
            }
        }

        public int OutputCount
        {
            get
            {
                return this.unspentTxOutputsCount;
            }
        }

        public bool ContainsOutput(TxOutputKey txOutputKey)
        {
            //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(this.jetSession, this.unspentTxOutputsTableId, DataEncoder.EncodeTxOutputKey(txOutputKey), MakeKeyGrbit.NewKey);
            return Api.TrySeek(this.jetSession, this.unspentTxOutputsTableId, SeekGrbit.SeekEQ);
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxOutputsTableId, "IX_TxOutputKey");
                Api.MakeKey(this.jetSession, this.unspentTxOutputsTableId, DataEncoder.EncodeTxOutputKey(txOutputKey), MakeKeyGrbit.NewKey);
                if (Api.TrySeek(this.jetSession, this.unspentTxOutputsTableId, SeekGrbit.SeekEQ))
                {
                    txOutput = DataEncoder.DecodeTxOutput(Api.RetrieveColumn(this.jetSession, this.unspentTxOutputsTableId, this.txOutputColumnId));
                    return true;
                }
                else
                {
                    txOutput = default(TxOutput);
                    return false;
                }
            }
            finally
            {
                Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
            }
        }

        public void AddOutput(TxOutputKey txOutputKey, TxOutput txOutput)
        {
            Api.JetBeginTransaction(this.jetSession);
            try
            {
                Api.JetPrepareUpdate(this.jetSession, this.unspentTxOutputsTableId, JET_prep.Insert);
                try
                {
                    Api.SetColumn(this.jetSession, this.unspentTxOutputsTableId, this.txOutputKeyColumnId, DataEncoder.EncodeTxOutputKey(txOutputKey));
                    Api.SetColumn(this.jetSession, this.unspentTxOutputsTableId, this.txOutputColumnId, DataEncoder.EncodeTxOutput(txOutput));

                    if (IndexOutputs)
                    {
                        var sha256 = new SHA256Managed();
                        Api.SetColumn(this.jetSession, this.unspentTxOutputsTableId, this.outputScriptHashColumnId, sha256.ComputeHash(txOutput.ScriptPublicKey.ToArray()));
                    }

                    Api.JetUpdate(this.jetSession, this.unspentTxOutputsTableId);
                    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                    this.unspentTxOutputsCount++;
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(this.jetSession, this.unspentTxOutputsTableId, JET_prep.Cancel);
                    throw;
                }
            }
            catch (Exception)
            {
                Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);
                throw;
            }
        }

        public bool RemoveOutput(TxOutputKey txOutputKey)
        {
            Api.JetBeginTransaction(this.jetSession);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxOutputsTableId, "IX_TxOutputKey");
                Api.MakeKey(this.jetSession, this.unspentTxOutputsTableId, DataEncoder.EncodeTxOutputKey(txOutputKey), MakeKeyGrbit.NewKey);
                if (!Api.TrySeek(this.jetSession, this.unspentTxOutputsTableId, SeekGrbit.SeekEQ))
                    throw new KeyNotFoundException();

                Api.JetDelete(this.jetSession, this.unspentTxOutputsTableId);
                Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                this.unspentTxOutputsCount--;
                return true;
            }
            catch (Exception)
            {
                Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);
                throw;
            }
        }

        public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> UnspentOutputs()
        {
            Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
            try
            {
                //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxOutputsTableId, "IX_TxOutputKey");
                Api.MoveBeforeFirst(this.jetSession, this.unspentTxOutputsTableId);
                while (Api.TryMoveNext(this.jetSession, this.unspentTxOutputsTableId))
                {
                    var txOutputKey = DataEncoder.DecodeTxOutputKey(Api.RetrieveColumn(this.jetSession, this.unspentTxOutputsTableId, this.txOutputKeyColumnId));
                    var txOutput = DataEncoder.DecodeTxOutput(Api.RetrieveColumn(this.jetSession, this.unspentTxOutputsTableId, this.txOutputColumnId));

                    yield return new KeyValuePair<TxOutputKey, TxOutput>(txOutputKey, txOutput);
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
            instance.Parameters.LogFileSize = 1024;
            instance.Parameters.LogBuffers = 1024;
            instance.Parameters.MaxTemporaryTables = 0;
            instance.Parameters.MaxVerPages = 1024;
            instance.Parameters.NoInformationEvent = true;
            instance.Parameters.WaypointLatency = 1;
            instance.Parameters.MaxSessions = 256;
            instance.Parameters.MaxOpenTables = 256;

            return instance;
        }

        private static Instance CreateInstance2(string directory)
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

        private static void CreateDatabase(string jetDatabase, Instance jetInstance)
        {
            JET_DBID utxoDbId;
            JET_TABLEID globalTableId;
            JET_COLUMNID blockHashColumnId;
            JET_TABLEID unspentTxTableId;
            JET_COLUMNID txHashColumnId;
            JET_COLUMNID confirmedBlockHashColumnId;
            JET_COLUMNID outputStatesColumnId;
            JET_TABLEID unspentTxOutputsTableId;
            JET_COLUMNID txOutputKeyColumnId;
            JET_COLUMNID txOutputColumnId;
            JET_COLUMNID outputScriptHashColumnId;

            using (var jetSession = new Session(jetInstance))
            {
                Api.JetCreateDatabase(jetSession, jetDatabase, "", out utxoDbId, CreateDatabaseGrbit.None);

                Api.JetCreateTable(jetSession, utxoDbId, "global", 0, 0, out globalTableId);
                Api.JetAddColumn(jetSession, globalTableId, "BlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockHashColumnId);

                Api.JetCreateTable(jetSession, utxoDbId, "unspentTx", 0, 0, out unspentTxTableId);
                Api.JetAddColumn(jetSession, unspentTxTableId, "TxHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out txHashColumnId);
                Api.JetAddColumn(jetSession, unspentTxTableId, "ConfirmedBlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out confirmedBlockHashColumnId);
                Api.JetAddColumn(jetSession, unspentTxTableId, "OutputStates", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out outputStatesColumnId);

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

                Api.JetCreateTable(jetSession, utxoDbId, "unspentTxOutputs", 0, 0, out unspentTxOutputsTableId);
                Api.JetAddColumn(jetSession, unspentTxOutputsTableId, "TxOutputKey", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 36, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out txOutputKeyColumnId);
                Api.JetAddColumn(jetSession, unspentTxOutputsTableId, "TxOutput", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out txOutputColumnId);
                Api.JetAddColumn(jetSession, unspentTxOutputsTableId, "OutputScriptHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32 }, null, 0, out outputScriptHashColumnId);

                Api.JetCreateIndex2(jetSession, unspentTxOutputsTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_TxOutputKey",
                            szKey = "+TxOutputKey\0\0",
                            cbKey = "+TxOutputKey\0\0".Length
                        }
                    }, 1);

                Api.JetCreateIndex2(jetSession, unspentTxOutputsTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexIgnoreNull,
                            szIndexName = "IX_OutputScriptHash",
                            szKey = "+OutputScriptHash\0\0",
                            cbKey = "+OutputScriptHash\0\0".Length
                        }
                    }, 1);

                Api.JetCloseTable(jetSession, globalTableId);
                Api.JetCloseTable(jetSession, unspentTxTableId);
                Api.JetCloseTable(jetSession, unspentTxOutputsTableId);
            }
        }

        private static void OpenDatabase(string jetDatabase, Instance jetInstance, bool readOnly,
            out Session jetSession,
            out JET_DBID utxoDbId,
            out JET_TABLEID globalTableId,
            out JET_COLUMNID blockHashColumnId,
            out JET_TABLEID unspentTxTableId,
            out JET_COLUMNID txHashColumnId,
            out JET_COLUMNID confirmedBlockHashColumnId,
            out JET_COLUMNID outputStatesColumnId,
            out JET_TABLEID unspentTxOutputsTableId,
            out JET_COLUMNID txOutputKeyColumnId,
            out JET_COLUMNID txOutputColumnId,
            out JET_COLUMNID outputScriptHashColumnId)
        {
            jetSession = new Session(jetInstance);

            Api.JetAttachDatabase(jetSession, jetDatabase, readOnly ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None);
            Api.JetOpenDatabase(jetSession, jetDatabase, "", out utxoDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);

            Api.JetOpenTable(jetSession, utxoDbId, "global", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out globalTableId);
            blockHashColumnId = Api.GetTableColumnid(jetSession, globalTableId, "BlockHash");

            Api.JetOpenTable(jetSession, utxoDbId, "unspentTx", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out unspentTxTableId);
            txHashColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "TxHash");
            confirmedBlockHashColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "ConfirmedBlockHash");
            outputStatesColumnId = Api.GetTableColumnid(jetSession, unspentTxTableId, "OutputStates");

            Api.JetOpenTable(jetSession, utxoDbId, "unspentTxOutputs", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out unspentTxOutputsTableId);
            txOutputKeyColumnId = Api.GetTableColumnid(jetSession, unspentTxOutputsTableId, "TxOutputKey");
            txOutputColumnId = Api.GetTableColumnid(jetSession, unspentTxOutputsTableId, "TxOutput");
            outputScriptHashColumnId = Api.GetTableColumnid(jetSession, unspentTxOutputsTableId, "OutputScriptHash");
        }
    }
}
