using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Core;
using Microsoft.Isam.Esent.Interop;
using System.IO;
using System.Threading;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Microsoft.Isam.Esent.Interop.Vista;
using Microsoft.Isam.Esent.Interop.Windows81;
using NLog;
using Microsoft.Isam.Esent.Interop.Windows8;

namespace BitSharp.Esent
{
    public class BlockTxesStorage : IBlockTxesStorage
    {
        private readonly Logger logger;
        private readonly string jetDirectory;
        private readonly string jetDatabase;
        private readonly Instance jetInstance;

        private readonly BlockTxesCursor[] cursors;
        private readonly object cursorsLock;

        public BlockTxesStorage(string baseDirectory, Logger logger)
        {
            this.logger = logger;
            this.jetDirectory = Path.Combine(baseDirectory, "Blocks");
            this.jetDatabase = Path.Combine(this.jetDirectory, "BlockTxes.edb");

            this.cursors = new BlockTxesCursor[16];
            this.cursorsLock = new object();

            this.jetInstance = CreateInstance(this.jetDirectory);
            this.jetInstance.Init();
            try
            {
                this.CreateOrOpenDatabase();
            }
            catch (Exception)
            {
                this.jetInstance.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            //TODO should i lock in dispose?
            lock (this.cursorsLock)
            {
                this.cursors.DisposeList();
            }

            this.jetInstance.Dispose();
        }

        internal Instance JetInstance { get { return this.jetInstance; } }

        public bool ContainsBlock(UInt256 blockHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                int blockId;
                return this.TryGetBlockId(cursor, blockHash, out blockId);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        private void AddTransaction(int blockId, int txIndex, UInt256 txHash, byte[] txBytes, BlockTxesCursor cursor)
        {
            using (var jetUpdate = cursor.jetSession.BeginUpdate(cursor.blocksTableId, JET_prep.Insert))
            {
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockIdColumnId, blockId);
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxIndexColumnId, txIndex);
                //TODO i'm using -1 depth to mean not pruned, this should be interpreted as depth 0
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockDepthColumnId, -1);
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxHashColumnId, DbEncoder.EncodeUInt256(txHash));
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxBytesColumnId, txBytes);

                jetUpdate.Save();
            }
        }

        public void PruneElements(UInt256 blockHash, IEnumerable<int> txIndices)
        {
            using (var pruningCursor = OpenPruningCursor(blockHash))
            //using (var cachedCursor = new CachedMerkleTreePruningCursor(pruningCursor))
            {
                pruningCursor.BeginTransaction();
                try
                {
                    // prune the transactions
                    foreach (var index in txIndices)
                        MerkleTree.PruneNode(pruningCursor, index);

                    pruningCursor.CommitTransaction();
                }
                catch (Exception)
                {
                    pruningCursor.RollbackTransaction();
                    throw;
                }
            }
        }

        public IMerkleTreePruningCursor OpenPruningCursor(UInt256 blockHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                int blockId;
                if (!this.TryGetBlockId(cursor, blockHash, out blockId))
                    throw new MissingDataException(blockHash);

                return new MerkleTreePruningCursor(blockId, cursor,
                    disposeAction: () => this.FreeCursor(cursor));
            }
            catch (Exception)
            {
                this.FreeCursor(cursor);
                throw;
            }
        }

        public bool TryReadBlockTransactions(UInt256 blockHash, out IEnumerable<BlockTx> blockTxes)
        {
            var cursor = this.OpenCursor();
            try
            {
                int blockId;
                if (this.TryGetBlockId(cursor, blockHash, out blockId))
                {
                    blockTxes = ReadBlockTransactions(blockHash, blockId);
                    return true;
                }
                else
                {
                    blockTxes = null;
                    return false;
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        private IEnumerable<BlockTx> ReadBlockTransactions(UInt256 blockHash, int blockId)
        {
            var cursor = this.OpenCursor();
            try
            {
                using (var jetTx = cursor.jetSession.BeginTransaction())
                {
                    var sha256 = new SHA256Managed();

                    Api.JetSetCurrentIndex(cursor.jetSession, cursor.blocksTableId, "IX_BlockIdTxIndex");
                    Api.MakeKey(cursor.jetSession, cursor.blocksTableId, blockId, MakeKeyGrbit.NewKey);
                    Api.MakeKey(cursor.jetSession, cursor.blocksTableId, -1, MakeKeyGrbit.None);

                    if (!Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekGE))
                        yield break;

                    do
                    {
                        if (blockId != Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockIdColumnId).Value)
                            yield break;

                        var txIndex = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockTxIndexColumnId).Value;
                        var depth = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockDepthColumnId).Value;
                        var txHash = DbEncoder.DecodeUInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxHashColumnId));
                        var txBytes = Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxBytesColumnId);

                        // determine if transaction is pruned by its depth
                        var pruned = depth >= 0;
                        depth = Math.Max(0, depth);

                        BitSharp.Core.Domain.Transaction tx;
                        if (!pruned)
                        {
                            // verify transaction is not corrupt
                            if (txHash != new UInt256(sha256.ComputeDoubleHash(txBytes)))
                                throw new MissingDataException(blockHash);

                            tx = DataEncoder.DecodeTransaction(txBytes);
                        }
                        else
                        {
                            tx = null;
                        }

                        var blockTx = new BlockTx(txIndex, depth, txHash, pruned, tx);

                        yield return blockTx;
                    } while (Api.TryMoveNext(cursor.jetSession, cursor.blocksTableId));
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public bool TryGetTransaction(UInt256 blockHash, int txIndex, out BitSharp.Core.Domain.Transaction transaction)
        {
            var cursor = this.OpenCursor();
            try
            {
                using (var jetTx = cursor.jetSession.BeginTransaction())
                {
                    int blockId;
                    if (!this.TryGetBlockId(cursor, blockHash, out blockId))
                    {
                        transaction = default(BitSharp.Core.Domain.Transaction);
                        return false;
                    }

                    Api.JetSetCurrentIndex(cursor.jetSession, cursor.blocksTableId, "IX_BlockIdTxIndex");
                    Api.MakeKey(cursor.jetSession, cursor.blocksTableId, blockId, MakeKeyGrbit.NewKey);
                    Api.MakeKey(cursor.jetSession, cursor.blocksTableId, txIndex, MakeKeyGrbit.None);
                    if (Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekEQ))
                    {
                        var txBytes = Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxBytesColumnId);
                        if (txBytes != null)
                        {
                            transaction = DataEncoder.DecodeTransaction(txBytes);
                            return true;
                        }
                        else
                        {
                            transaction = default(BitSharp.Core.Domain.Transaction);
                            return false;
                        }
                    }
                    else
                    {
                        transaction = default(BitSharp.Core.Domain.Transaction);
                        return false;
                    }
                }
            }
            finally
            {
                this.FreeCursor(cursor);
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

        private void CreateOrOpenDatabase()
        {
            try
            {
                OpenDatabase();
            }
            catch (Exception)
            {
                DeleteDatabase();
                CreateDatabase();
            }
        }

        private void CreateDatabase()
        {
            JET_DBID blockDbId;
            JET_TABLEID globalsTableId;
            JET_COLUMNID blockCountColumnId;
            JET_COLUMNID flushColumnId;
            JET_TABLEID blockIdsTableId;
            JET_COLUMNID blockIdsHashColumnId;
            JET_COLUMNID blockIdsIdColumnId;
            JET_TABLEID blocksTableId;
            JET_COLUMNID blockIdColumnId;
            JET_COLUMNID blockTxIndexColumnId;
            JET_COLUMNID blockDepthColumnId;
            JET_COLUMNID blockTxHashColumnId;
            JET_COLUMNID blockTxBytesColumnId;

            using (var jetSession = new Session(this.jetInstance))
            {
                Api.JetCreateDatabase(jetSession, this.jetDatabase, "", out blockDbId, CreateDatabaseGrbit.None);

                var defaultValue = BitConverter.GetBytes(0);
                Api.JetCreateTable(jetSession, blockDbId, "Globals", 0, 0, out globalsTableId);
                Api.JetAddColumn(jetSession, globalsTableId, "BlockCount", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnEscrowUpdate }, defaultValue, defaultValue.Length, out blockCountColumnId);
                Api.JetAddColumn(jetSession, globalsTableId, "Flush", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnEscrowUpdate }, defaultValue, defaultValue.Length, out flushColumnId);

                // initialize global data
                using (var jetUpdate = jetSession.BeginUpdate(globalsTableId, JET_prep.Insert))
                {
                    Api.SetColumn(jetSession, globalsTableId, blockCountColumnId, 0);
                    Api.SetColumn(jetSession, globalsTableId, flushColumnId, 0);

                    jetUpdate.Save();
                }

                Api.JetCloseTable(jetSession, globalsTableId);

                Api.JetCreateTable(jetSession, blockDbId, "BlockIds", 0, 0, out blockIdsTableId);
                Api.JetAddColumn(jetSession, blockIdsTableId, "BlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockIdsHashColumnId);
                Api.JetAddColumn(jetSession, blockIdsTableId, "BlockId", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnAutoincrement | ColumndefGrbit.ColumnNotNULL }, null, 0, out blockIdsIdColumnId);

                Api.JetCreateIndex2(jetSession, blockIdsTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexUnique | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_BlockHash",
                            szKey = "+BlockHash\0\0",
                            cbKey = "+BlockHash\0\0".Length
                        }
                    }, 1);

                Api.JetCloseTable(jetSession, blockIdsTableId);

                Api.JetCreateTable(jetSession, blockDbId, "Blocks", 0, 0, out blocksTableId);
                Api.JetAddColumn(jetSession, blocksTableId, "BlockId", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out blockIdColumnId);
                Api.JetAddColumn(jetSession, blocksTableId, "TxIndex", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out blockTxIndexColumnId);
                Api.JetAddColumn(jetSession, blocksTableId, "Depth", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out blockDepthColumnId);
                Api.JetAddColumn(jetSession, blocksTableId, "TxHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockTxHashColumnId);
                Api.JetAddColumn(jetSession, blocksTableId, "TxBytes", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary }, null, 0, out blockTxBytesColumnId);

                Api.JetCreateIndex2(jetSession, blocksTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexUnique | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_BlockIdTxIndex",
                            szKey = "+BlockId\0+TxIndex\0\0",
                            cbKey = "+BlockId\0+TxIndex\0\0".Length
                        }
                    }, 1);

                Api.JetCloseTable(jetSession, blocksTableId);
            }
        }

        private void DeleteDatabase()
        {
            try { Directory.Delete(this.jetDirectory, recursive: true); }
            catch (Exception) { }
            Directory.CreateDirectory(this.jetDirectory);
        }

        private void OpenDatabase()
        {
            var readOnly = false;

            using (var jetSession = new Session(this.jetInstance))
            {
                Api.JetAttachDatabase(jetSession, this.jetDatabase, readOnly ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None);
                try
                {
                    JET_DBID blockDbId;
                    Api.JetOpenDatabase(jetSession, this.jetDatabase, "", out blockDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);
                    try
                    {
                        var cursor = this.OpenCursor();
                        try
                        {
                            // reset flush column
                            using (var jetUpdate = cursor.jetSession.BeginUpdate(cursor.globalsTableId, JET_prep.Replace))
                            {
                                Api.SetColumn(cursor.jetSession, cursor.globalsTableId, cursor.flushColumnId, 0);

                                jetUpdate.Save();
                            }
                        }
                        finally
                        {
                            this.FreeCursor(cursor);
                        }
                    }
                    catch (Exception)
                    {
                        Api.JetCloseDatabase(jetSession, blockDbId, CloseDatabaseGrbit.None);
                        throw;
                    }
                }
                catch (Exception)
                {
                    Api.JetDetachDatabase(jetSession, this.jetDatabase);
                    throw;
                }
            }
        }

        public int BlockCount
        {
            get
            {
                var cursor = this.OpenCursor();
                try
                {
                    return Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.globalsTableId, cursor.blockCountColumnId).Value;
                }
                finally
                {
                    this.FreeCursor(cursor);
                }
            }
        }

        public string Name
        {
            get { return "Blocks"; }
        }

        public bool TryAddBlockTransactions(UInt256 blockHash, IEnumerable<BitSharp.Core.Domain.Transaction> blockTxes)
        {
            try
            {
                var cursor = this.OpenCursor();
                try
                {
                    int blockId;
                    if (this.TryGetBlockId(cursor, blockHash, out blockId))
                        return false;

                    using (var jetTx = cursor.jetSession.BeginTransaction())
                    {
                        using (var jetUpdate = cursor.jetSession.BeginUpdate(cursor.blockIdsTableId, JET_prep.Insert))
                        {
                            blockId = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blockIdsTableId, cursor.blockIdsIdColumnId, RetrieveColumnGrbit.RetrieveCopy).Value;
                            Api.SetColumn(cursor.jetSession, cursor.blockIdsTableId, cursor.blockIdsHashColumnId, DbEncoder.EncodeUInt256(blockHash));

                            jetUpdate.Save();
                        }

                        var txIndex = 0;
                        foreach (var tx in blockTxes)
                        {
                            AddTransaction(blockId, txIndex, tx.Hash, DataEncoder.EncodeTransaction(tx), cursor);
                            txIndex++;
                        }

                        // increase block count
                        Api.EscrowUpdate(cursor.jetSession, cursor.globalsTableId, cursor.blockCountColumnId, +1);

                        jetTx.CommitLazy();
                        return true;
                    }
                }
                finally
                {
                    this.FreeCursor(cursor);
                }
            }
            catch (EsentKeyDuplicateException)
            {
                return false;
            }
        }

        public bool TryRemoveBlockTransactions(UInt256 blockHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                using (var jetTx = cursor.jetSession.BeginTransaction())
                {
                    Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockIdsTableId, "IX_BlockHash");
                    Api.MakeKey(cursor.jetSession, cursor.blockIdsTableId, DbEncoder.EncodeUInt256(blockHash), MakeKeyGrbit.NewKey);

                    if (Api.TrySeek(cursor.jetSession, cursor.blockIdsTableId, SeekGrbit.SeekEQ))
                    {
                        var blockId = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blockIdsTableId, cursor.blockIdsIdColumnId).Value;

                        // remove block id
                        Api.JetDelete(cursor.jetSession, cursor.blockIdsTableId);

                        // remove transactions
                        Api.JetSetCurrentIndex(cursor.jetSession, cursor.blocksTableId, "IX_BlockIdTxIndex");
                        Api.MakeKey(cursor.jetSession, cursor.blocksTableId, blockId, MakeKeyGrbit.NewKey);
                        Api.MakeKey(cursor.jetSession, cursor.blocksTableId, -1, MakeKeyGrbit.None);

                        if (Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekGE)
                            && blockId == Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockIdColumnId).Value)
                        {
                            do
                            {
                                Api.JetDelete(cursor.jetSession, cursor.blocksTableId);
                            } while (Api.TryMoveNext(cursor.jetSession, cursor.blocksTableId)
                                && blockId == Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockIdColumnId).Value);
                        }

                        // decrease block count
                        Api.EscrowUpdate(cursor.jetSession, cursor.globalsTableId, cursor.blockCountColumnId, -1);

                        jetTx.CommitLazy();
                        return true;
                    }
                    else
                    {
                        // transactions are already removed
                        return false;
                    }
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public void Flush()
        {
            var cursor = this.OpenCursor();
            try
            {
                using (var jetTx = cursor.jetSession.BeginTransaction())
                {
                    Api.EscrowUpdate(cursor.jetSession, cursor.globalsTableId, cursor.flushColumnId, 1);
                    jetTx.Commit(CommitTransactionGrbit.None);
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public void Defragment()
        {
            var cursor = this.OpenCursor();
            try
            {
                //int passes = -1, seconds = -1;
                //Api.JetDefragment(cursor.jetSession, cursor.blockDbId, "BlockTxes", ref passes, ref seconds, DefragGrbit.BatchStart);

                if (EsentVersion.SupportsWindows81Features)
                {
                    this.logger.Info("Begin shrinking block txes database");

                    int actualPages;
                    Windows8Api.JetResizeDatabase(cursor.jetSession, cursor.blockDbId, 0, out actualPages, Windows81Grbits.OnlyShrink);

                    this.logger.Info("Finished shrinking block txes database: {0:#,##0} MB".Format2((float)actualPages * SystemParameters.DatabasePageSize / 1.MILLION()));
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        private BlockTxesCursor OpenCursor()
        {
            BlockTxesCursor cursor = null;

            lock (this.cursorsLock)
            {
                for (var i = 0; i < this.cursors.Length; i++)
                {
                    if (this.cursors[i] != null)
                    {
                        cursor = this.cursors[i];
                        this.cursors[i] = null;
                        break;
                    }
                }
            }

            if (cursor == null)
                cursor = new BlockTxesCursor(this.jetDatabase, this.jetInstance);

            return cursor;
        }

        private void FreeCursor(BlockTxesCursor cursor)
        {
            var cached = false;

            lock (this.cursorsLock)
            {
                for (var i = 0; i < this.cursors.Length; i++)
                {
                    if (this.cursors[i] == null)
                    {
                        this.cursors[i] = cursor;
                        cached = true;
                        break;
                    }
                }
            }

            if (!cached)
                cursor.Dispose();
        }

        private bool TryGetBlockId(BlockTxesCursor cursor, UInt256 blockHash, out int blockId)
        {
            Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockIdsTableId, "IX_BlockHash");
            Api.MakeKey(cursor.jetSession, cursor.blockIdsTableId, DbEncoder.EncodeUInt256(blockHash), MakeKeyGrbit.NewKey);
            if (Api.TrySeek(cursor.jetSession, cursor.blockIdsTableId, SeekGrbit.SeekEQ))
            {
                blockId = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blockIdsTableId, cursor.blockIdsIdColumnId).Value;
                return true;

            }
            else
            {
                blockId = default(int);
                return false;
            }
        }
    }
}
