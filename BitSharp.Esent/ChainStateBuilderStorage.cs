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
    //TODO this should eventually be written directly against the Jet API so that everything can be tuned specifically to the UTXO
    public class ChainStateBuilderStorage : IChainStateBuilderStorage
    {
        //TODO
        public static bool IndexOutputs { get; set; }

        private readonly Logger logger;
        private bool closed = false;

        private readonly string jetDirectory;
        private readonly string jetDatabase;
        private readonly Instance jetInstance;
        private readonly Session jetSession;

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

        public ChainStateBuilderStorage(IChainStateStorage parentUtxo, Logger logger)
        {
            this.logger = logger;
            this.jetDirectory = GetDirectory();
            this.jetDatabase = Path.Combine(this.jetDirectory, "UTXO.edb");

            //TODO currently configured by PersistentDictionary
            //SystemParameters.DatabasePageSize = 8192;
            //var maxDbSizeInPages = 500.MILLION() / SystemParameters.DatabasePageSize;

            this.jetInstance = CreateInstance2(this.jetDirectory);
            this.jetInstance.Init();

            this.jetSession = new Session(this.jetInstance);

            Api.JetCreateDatabase(this.jetSession, this.jetDatabase, "", out this.utxoDbId, CreateDatabaseGrbit.None);
            //Api.JetAttachDatabase(this.jetSession, this.jetDatabase, AttachDatabaseGrbit.None);
            //Api.JetOpenDatabase(this.jetSession, this.jetDatabase, "", out this.utxoDbId, OpenDatabaseGrbit.Exclusive);

            Api.JetCreateTable(this.jetSession, this.utxoDbId, "global", 0, 0, out this.globalTableId);

            Api.JetAddColumn(this.jetSession, this.globalTableId, "BlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out this.blockHashColumnId);

            Api.JetCreateTable(this.jetSession, this.utxoDbId, "unspentTx", 0, 0, out this.unspentTxTableId);

            Api.JetAddColumn(this.jetSession, this.unspentTxTableId, "TxHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out this.txHashColumnId);
            Api.JetAddColumn(this.jetSession, this.unspentTxTableId, "ConfirmedBlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out this.confirmedBlockHashColumnId);
            Api.JetAddColumn(this.jetSession, this.unspentTxTableId, "OutputStates", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out this.outputStatesColumnId);

            Api.JetCreateIndex2(this.jetSession, this.unspentTxTableId,
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

            Api.JetCreateTable(this.jetSession, this.utxoDbId, "unspentTxOutputs", 0, 0, out unspentTxOutputsTableId);

            Api.JetAddColumn(this.jetSession, this.unspentTxOutputsTableId, "TxOutputKey", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 36, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out this.txOutputKeyColumnId);
            Api.JetAddColumn(this.jetSession, this.unspentTxOutputsTableId, "TxOutput", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out this.txOutputColumnId);
            Api.JetAddColumn(this.jetSession, this.unspentTxOutputsTableId, "OutputScriptHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32 }, null, 0, out this.outputScriptHashColumnId);

            Api.JetCreateIndex2(this.jetSession, this.unspentTxOutputsTableId,
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

            Api.JetCreateIndex2(this.jetSession, this.unspentTxOutputsTableId,
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

            this.BlockHash = parentUtxo.BlockHash;

            foreach (var unspentTx in parentUtxo.UnspentTransactions())
                this.AddTransaction(unspentTx.Key, unspentTx.Value);

            foreach (var unspentOutput in parentUtxo.UnspentOutputs())
                this.AddOutput(unspentOutput.Key, unspentOutput.Value);
        }

        ~ChainStateBuilderStorage()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            this.jetSession.Dispose();
            this.jetInstance.Dispose();

            if (!this.closed)
            {
                ChainStateStorage.DeleteUtxoDirectory(this.jetDirectory);
                this.closed = true;
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

        public IChainStateStorage ToImmutable(UInt256 blockHash)
        {
            throw new NotImplementedException();

            ////TODO see if JetOSSnapshotPrepare for taking snapshots here

            //var stopwatch = new Stopwatch();
            //stopwatch.Start();

            //// prepare destination directory
            //var destPath = UtxoStorage.GetDirectory(blockHash);
            //UtxoStorage.DeleteUtxoDirectory(destPath);
            //Directory.CreateDirectory(destPath + "_tx");
            //Directory.CreateDirectory(destPath + "_output");

            //// get database instances
            //var instanceField = typeof(PersistentDictionary<string, string>)
            //    .GetField("instance", BindingFlags.NonPublic | BindingFlags.Instance);
            //var unspentTransactionsInstance = (Instance)instanceField.GetValue(this.unspentTransactions.RawDictionary);
            //var unspentOutputsInstance = (Instance)instanceField.GetValue(this.unspentOutputs.RawDictionary);

            //// backup to temporary directory
            //using (var session = new Session(unspentTransactionsInstance))
            //    Api.JetBackupInstance(unspentTransactionsInstance, this.directory + "_tx_snapshot", BackupGrbit.Atomic, null);
            //using (var session = new Session(unspentOutputsInstance))
            //    Api.JetBackupInstance(unspentOutputsInstance, this.directory + "_output_snapshot", BackupGrbit.Atomic, null);

            //// restore to destination directory
            //using (var instance = CreateInstance(destPath + "_tx"))
            //    Api.JetRestoreInstance(instance, this.directory + "_tx_snapshot", destPath + "_tx", null);
            //using (var instance = CreateInstance(destPath + "_output"))
            //    Api.JetRestoreInstance(instance, this.directory + "_output_snapshot", destPath + "_output", null);

            //// cleanup temporary directory
            //try { Directory.Delete(this.directory + "_tx_snapshot", recursive: true); }
            //catch (Exception) { }
            //try { Directory.Delete(this.directory + "_output_snapshot", recursive: true); }
            //catch (Exception) { }

            //// initialize saved utxo
            //var utxoStorage = new UtxoStorage(blockHash);

            //// verify restore
            //if (utxoStorage.TransactionCount != this.TransactionCount)
            //    throw new Exception("TODO");
            //if (utxoStorage.OutputCount != this.OutputCount)
            //    throw new Exception("TODO");

            //// log snapshot info
            //this.logger.Info("Saved UTXO snapshot for {0} in: {1}".Format2(blockHash, stopwatch.Elapsed));

            //// return saved utxo
            //return utxoStorage;
        }

        public IChainStateStorage Close(UInt256 blockHash)
        {
            throw new NotImplementedException();

            //// close builder
            //this.closed = true;
            //this.Dispose();

            //// prepare destination directory
            //var destPath = UtxoStorage.GetDirectory(blockHash);
            //UtxoStorage.DeleteUtxoDirectory(destPath);
            //Directory.CreateDirectory(destPath + "_tx");
            //Directory.CreateDirectory(destPath + "_output");

            ////TOOD can something with JetCompact be done here to save off optimized UTXO snapshots?
            ////TODO once the blockchain is synced, see if this can be used to shrink used space during normal operation

            //// move utxo to new location
            //foreach (var srcFile in Directory.GetFiles(this.directory + "_tx", "*.edb"))
            //    File.Move(srcFile, Path.Combine(destPath + "_tx", Path.GetFileName(srcFile)));
            //foreach (var srcFile in Directory.GetFiles(this.directory + "_output", "*.edb"))
            //    File.Move(srcFile, Path.Combine(destPath + "_output", Path.GetFileName(srcFile)));
            //UtxoStorage.DeleteUtxoDirectory(this.directory);

            //// return saved utxo
            //return new UtxoStorage(blockHash);
        }

        private string GetDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "utxo", Guid.NewGuid().ToString());
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
            Api.JetBeginTransaction(this.jetSession);
        }

        public void CommitTransaction()
        {
            Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
        }

        public void RollbackTransaction()
        {
            Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);
        }
    }
}
