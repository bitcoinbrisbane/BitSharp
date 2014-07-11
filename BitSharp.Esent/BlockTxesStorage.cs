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

namespace BitSharp.Esent
{
    public class BlockTxesStorage : IBlockTxesStorage
    {
        private readonly string jetDirectory;
        private readonly string jetDatabase;
        private readonly Instance jetInstance;

        private readonly BlockTxesCursor[] cursors;
        private readonly object cursorsLock;

        public BlockTxesStorage(string baseDirectory)
        {
            this.jetDirectory = Path.Combine(baseDirectory, "Blocks");
            this.jetDatabase = Path.Combine(this.jetDirectory, "BlockTxes.edb");

            this.cursors = new BlockTxesCursor[64];
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
                Api.JetSetCurrentIndex(cursor.jetSession, cursor.blocksTableId, "IX_BlockHashTxIndex");
                Api.MakeKey(cursor.jetSession, cursor.blocksTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                Api.MakeKey(cursor.jetSession, cursor.blocksTableId, -1, MakeKeyGrbit.None);

                if (Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekGE)
                    && blockHash == new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId)))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public void AddBlock(Block block)
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetBeginTransaction(cursor.jetSession);
                try
                {
                    for (var txIndex = 0; txIndex < block.Transactions.Length; txIndex++)
                    {
                        var tx = block.Transactions[txIndex];
                        AddTransaction(block.Hash, txIndex, tx.Hash, DataEncoder.EncodeTransaction(tx), cursor);
                    }

                    Api.JetCommitTransaction(cursor.jetSession, CommitTransactionGrbit.LazyFlush);
                }
                catch (Exception)
                {
                    Api.JetRollback(cursor.jetSession, RollbackTransactionGrbit.None);
                    throw;
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        private void AddTransaction(UInt256 blockHash, int txIndex, UInt256 txHash, byte[] txBytes, BlockTxesCursor cursor)
        {
            Api.JetPrepareUpdate(cursor.jetSession, cursor.blocksTableId, JET_prep.Insert);
            try
            {
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId, blockHash.ToByteArray());
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxIndexColumnId, txIndex);
                //TODO i'm using -1 depth to mean not pruned, this should be interpreted as depth 0
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockDepthColumnId, -1);
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxHashColumnId, txHash.ToByteArray());
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxBytesColumnId, txBytes);

                Api.JetUpdate(cursor.jetSession, cursor.blocksTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(cursor.jetSession, cursor.blocksTableId, JET_prep.Cancel);
                throw;
            }
        }

        public void PruneElements(UInt256 blockHash, IEnumerable<int> indices)
        {
            using (var merkleWalker = OpenWalker(blockHash))
            {
                merkleWalker.BeginTransaction();

                foreach (var index in indices)
                    DataCalculatorNew.PruneNode(merkleWalker, index);

                merkleWalker.CommitTransaction();
            }
        }

        public IMerkleTreePruningCursor OpenWalker(UInt256 blockHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                return new MerkleTreePruningCursor(blockHash, cursor,
                    disposeAction: () => this.FreeCursor(cursor));
            }
            catch (Exception)
            {
                this.FreeCursor(cursor);
                throw;
            }
        }

        public IEnumerable<BlockTx> ReadBlockTransactions(UInt256 blockHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                var sha256 = new SHA256Managed();

                Api.JetBeginTransaction2(cursor.jetSession, BeginTransactionGrbit.ReadOnly);
                try
                {
                    Api.JetSetCurrentIndex(cursor.jetSession, cursor.blocksTableId, "IX_BlockHashTxIndex");
                    Api.MakeKey(cursor.jetSession, cursor.blocksTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                    Api.MakeKey(cursor.jetSession, cursor.blocksTableId, -1, MakeKeyGrbit.None);
                    if (Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekGE))
                    {
                        // perform an initial block hash check to see if at least one element was found
                        if (blockHash != new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId)))
                            throw new MissingDataException(blockHash);

                        do
                        {
                            if (blockHash != new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId)))
                                break;

                            var txIndex = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockTxIndexColumnId).Value;
                            var depth = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockDepthColumnId).Value;
                            var txHash = new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxHashColumnId));
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
                    else
                    {
                        throw new MissingDataException(blockHash);
                    }
                }
                finally
                {
                    Api.JetCommitTransaction(cursor.jetSession, CommitTransactionGrbit.LazyFlush);
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
                Api.JetBeginTransaction2(cursor.jetSession, BeginTransactionGrbit.ReadOnly);
                try
                {
                    Api.JetSetCurrentIndex(cursor.jetSession, cursor.blocksTableId, "IX_BlockHashTxIndex");
                    Api.MakeKey(cursor.jetSession, cursor.blocksTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                    Api.MakeKey(cursor.jetSession, cursor.blocksTableId, txIndex, MakeKeyGrbit.None);
                    if (Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekEQ))
                    {
                        transaction = DataEncoder.DecodeTransaction(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxBytesColumnId));
                        return true;
                    }
                    else
                    {
                        transaction = default(BitSharp.Core.Domain.Transaction);
                        return false;
                    }
                }
                finally
                {
                    Api.JetCommitTransaction(cursor.jetSession, CommitTransactionGrbit.LazyFlush);
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

            return instance;
        }

        //public void BeginTransaction()
        //{
        //    if (this.inTransaction)
        //        throw new InvalidOperationException();

        //    Api.JetBeginTransaction(this.jetSession);
        //    this.inTransaction = true;
        //}

        //public void CommitTransaction()
        //{
        //    if (!this.inTransaction)
        //        throw new InvalidOperationException();

        //    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
        //    this.inTransaction = false;
        //}

        //public void RollbackTransaction()
        //{
        //    if (!this.inTransaction)
        //        throw new InvalidOperationException();

        //    Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);
        //    this.inTransaction = false;
        //}

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
            JET_COLUMNID flushColumnId;
            JET_TABLEID blocksTableId;
            JET_COLUMNID blockHashColumnId;
            JET_COLUMNID blockTxIndexColumnId;
            JET_COLUMNID blockDepthColumnId;
            JET_COLUMNID blockTxHashColumnId;
            JET_COLUMNID blockTxBytesColumnId;

            using (var jetSession = new Session(this.jetInstance))
            {
                Api.JetCreateDatabase(jetSession, this.jetDatabase, "", out blockDbId, CreateDatabaseGrbit.None);

                var defaultValue = BitConverter.GetBytes(0);
                Api.JetCreateTable(jetSession, blockDbId, "global", 0, 0, out globalsTableId);
                Api.JetAddColumn(jetSession, globalsTableId, "Flush", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnEscrowUpdate }, defaultValue, defaultValue.Length, out flushColumnId);
                Api.JetCloseTable(jetSession, globalsTableId);

                Api.JetCreateTable(jetSession, blockDbId, "Blocks", 0, 0, out blocksTableId);
                Api.JetAddColumn(jetSession, blocksTableId, "BlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockHashColumnId);
                Api.JetAddColumn(jetSession, blocksTableId, "TxIndex", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockTxIndexColumnId);
                Api.JetAddColumn(jetSession, blocksTableId, "Depth", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockDepthColumnId);
                Api.JetAddColumn(jetSession, blocksTableId, "TxHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockTxHashColumnId);
                Api.JetAddColumn(jetSession, blocksTableId, "TxBytes", new JET_COLUMNDEF { coltyp = JET_coltyp.LongBinary }, null, 0, out blockTxBytesColumnId);

                Api.JetCreateIndex2(jetSession, blocksTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexUnique | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_BlockHashTxIndex",
                            szKey = "+BlockHash\0+TxIndex\0\0",
                            cbKey = "+BlockHash\0+TxIndex\0\0".Length
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
                            Api.JetBeginTransaction(cursor.jetSession);
                            try
                            {
                                if (Api.TryMoveFirst(cursor.jetSession, cursor.globalTableId))
                                    Api.JetPrepareUpdate(cursor.jetSession, cursor.globalTableId, JET_prep.Replace);
                                else
                                    Api.JetPrepareUpdate(cursor.jetSession, cursor.globalTableId, JET_prep.Insert);
                                try
                                {
                                    Api.SetColumn(cursor.jetSession, cursor.globalTableId, cursor.flushColumnId, 0);
                                    Api.JetUpdate(cursor.jetSession, cursor.globalTableId);

                                    Api.JetCommitTransaction(cursor.jetSession, CommitTransactionGrbit.None);
                                }
                                catch (Exception)
                                {
                                    Api.JetPrepareUpdate(cursor.jetSession, cursor.globalTableId, JET_prep.Cancel);
                                    throw;
                                }
                            }
                            catch (Exception)
                            {
                                Api.JetRollback(cursor.jetSession, RollbackTransactionGrbit.None);
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

        public int Count
        {
            get { return 0; }
        }

        public string Name
        {
            get { return "Blocks"; }
        }

        public bool TryAdd(UInt256 blockHash, Block block)
        {
            try
            {
                AddBlock(block);
                return true;
            }
            catch (EsentKeyDuplicateException)
            {
                return false;
            }
        }

        public bool TryRemove(UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public void Flush()
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetBeginTransaction(cursor.jetSession);
                try
                {
                    Api.EscrowUpdate(cursor.jetSession, cursor.globalTableId, cursor.flushColumnId, 1);
                    Api.JetCommitTransaction(cursor.jetSession, CommitTransactionGrbit.None);
                }
                catch (Exception)
                {
                    Api.JetRollback(cursor.jetSession, RollbackTransactionGrbit.None);
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
    }
}
