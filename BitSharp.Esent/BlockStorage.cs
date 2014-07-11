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
    public class BlockStorage : IBlockStorage
    {
        private readonly string jetDirectory;
        private readonly string jetDatabase;
        private readonly Instance jetInstance;

        private readonly BlockCursor[] cursors;
        private readonly object cursorsLock;

        public BlockStorage(string baseDirectory, Instance jetInstance)
        {
            this.jetDirectory = Path.Combine(baseDirectory, "Blocks");
            this.jetDatabase = Path.Combine(this.jetDirectory, "Blocks.edb");

            this.cursors = new BlockCursor[64];
            this.cursorsLock = new object();

            this.jetInstance = jetInstance;

            this.CreateOrOpenDatabase();
        }

        public void Dispose()
        {
            //TODO should i lock in dispose?
            lock (this.cursorsLock)
            {
                this.cursors.DisposeList();
            }
        }

        public bool ContainsChainedHeader(UInt256 blockHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockHeadersTableId, "IX_BlockHash");
                Api.MakeKey(cursor.jetSession, cursor.blockHeadersTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);

                return Api.TrySeek(cursor.jetSession, cursor.blockHeadersTableId, SeekGrbit.SeekEQ);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public bool TryAddChainedHeader(ChainedHeader chainedHeader)
        {
            try
            {
                var cursor = this.OpenCursor();
                try
                {
                    Api.JetPrepareUpdate(cursor.jetSession, cursor.blockHeadersTableId, JET_prep.Insert);
                    try
                    {
                        Api.SetColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderHashColumnId, chainedHeader.Hash.ToByteArray());
                        Api.SetColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderPreviousHashColumnId, chainedHeader.PreviousBlockHash.ToByteArray());
                        Api.SetColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderHeightColumnId, chainedHeader.Height);
                        Api.SetColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderTotalWorkColumnId, DataEncoder.EncodeTotalWork(chainedHeader.TotalWork));
                        Api.SetColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderBytesColumnId, DataEncoder.EncodeChainedHeader(chainedHeader));

                        Api.JetUpdate(cursor.jetSession, cursor.blockHeadersTableId);
                    }
                    catch (Exception)
                    {
                        Api.JetPrepareUpdate(cursor.jetSession, cursor.blockHeadersTableId, JET_prep.Cancel);
                        throw;
                    }

                    return true;
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

        public bool TryGetChainedHeader(UInt256 blockHash, out ChainedHeader chainedHeader)
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockHeadersTableId, "IX_BlockHash");
                Api.MakeKey(cursor.jetSession, cursor.blockHeadersTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                if (Api.TrySeek(cursor.jetSession, cursor.blockHeadersTableId, SeekGrbit.SeekEQ))
                {
                    chainedHeader = DataEncoder.DecodeChainedHeader(Api.RetrieveColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderBytesColumnId));
                    return true;
                }
                else
                {
                    chainedHeader = default(ChainedHeader);
                    return false;
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public ChainedHeader FindMaxTotalWork()
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockHeadersTableId, "IX_TotalWork");

                // IX_TotalWork is in reverse order, so higher total work comes first
                if (Api.TryMoveFirst(cursor.jetSession, cursor.blockHeadersTableId))
                {
                    do
                    {
                        // check if this block is valid
                        var valid = Api.RetrieveColumnAsBoolean(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderValidColumnId);
                        if (valid == null || valid.Value)
                        {
                            // decode chained header with most work and return
                            var chainedHeader = DataEncoder.DecodeChainedHeader(Api.RetrieveColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderBytesColumnId));
                            return chainedHeader;
                        }
                    } while (Api.TryMove(cursor.jetSession, cursor.blockHeadersTableId, JET_Move.Next, MoveGrbit.None));
                }

                // no valid chained header found
                return null;
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public IEnumerable<ChainedHeader> ReadChainedHeaders()
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockHeadersTableId, "IX_BlockHash");

                if (Api.TryMoveFirst(cursor.jetSession, cursor.blockHeadersTableId))
                {
                    do
                    {
                        // decode chained header and return
                        var chainedHeader = DataEncoder.DecodeChainedHeader(Api.RetrieveColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderBytesColumnId));
                        yield return chainedHeader;
                    } while (Api.TryMove(cursor.jetSession, cursor.blockHeadersTableId, JET_Move.Next, MoveGrbit.None));
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public void MarkBlockInvalid(UInt256 blockHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockHeadersTableId, "IX_BlockHash");
                Api.MakeKey(cursor.jetSession, cursor.blockHeadersTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);

                if (!Api.TrySeek(cursor.jetSession, cursor.blockHeadersTableId, SeekGrbit.SeekEQ))
                    throw new MissingDataException(blockHash);

                Api.JetPrepareUpdate(cursor.jetSession, cursor.blockHeadersTableId, JET_prep.Replace);
                try
                {
                    Api.SetColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderValidColumnId, true);

                    Api.JetUpdate(cursor.jetSession, cursor.blockHeadersTableId);
                }
                catch (Exception)
                {
                    Api.JetPrepareUpdate(cursor.jetSession, cursor.blockHeadersTableId, JET_prep.Cancel);
                    throw;
                }
            }
            finally
            {
                this.FreeCursor(cursor);
            }
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
            JET_COLUMNID flushColumnId;
            JET_TABLEID blockHeadersTableId;
            JET_COLUMNID blockHeaderHashColumnId;
            JET_COLUMNID blockHeaderPreviousHashColumnId;
            JET_COLUMNID blockHeaderHeightColumnId;
            JET_COLUMNID blockHeaderTotalWorkColumnId;
            JET_COLUMNID blockHeaderValidColumnId;
            JET_COLUMNID blockHeaderBytesColumnId;

            using (var jetSession = new Session(this.jetInstance))
            {
                Api.JetCreateDatabase(jetSession, jetDatabase, "", out blockDbId, CreateDatabaseGrbit.None);

                var defaultValue = BitConverter.GetBytes(0);
                Api.JetCreateTable(jetSession, blockDbId, "global", 0, 0, out globalsTableId);
                Api.JetAddColumn(jetSession, globalsTableId, "Flush", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnEscrowUpdate }, defaultValue, defaultValue.Length, out flushColumnId);
                Api.JetCloseTable(jetSession, globalsTableId);

                Api.JetCreateTable(jetSession, blockDbId, "BlockHeaders", 0, 0, out blockHeadersTableId);
                Api.JetAddColumn(jetSession, blockHeadersTableId, "BlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockHeaderHashColumnId);
                Api.JetAddColumn(jetSession, blockHeadersTableId, "PreviousBlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockHeaderPreviousHashColumnId);
                Api.JetAddColumn(jetSession, blockHeadersTableId, "Height", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out blockHeaderHeightColumnId);
                Api.JetAddColumn(jetSession, blockHeadersTableId, "TotalWork", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, grbit = ColumndefGrbit.ColumnNotNULL }, null, 0, out blockHeaderTotalWorkColumnId);
                Api.JetAddColumn(jetSession, blockHeadersTableId, "Valid", new JET_COLUMNDEF { coltyp = JET_coltyp.Bit, }, null, 0, out blockHeaderValidColumnId);
                Api.JetAddColumn(jetSession, blockHeadersTableId, "BlockHeaderBytes", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary }, null, 0, out blockHeaderBytesColumnId);

                Api.JetCreateIndex2(jetSession, blockHeadersTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_BlockHash",
                            szKey = "+BlockHash\0\0",
                            cbKey = "+BlockHash\0\0".Length
                        }
                    }, 1);

                //Api.JetCreateIndex2(jetSession, blockHeadersTableId,
                //    new JET_INDEXCREATE[]
                //    {
                //        new JET_INDEXCREATE
                //        {
                //            cbKeyMost = 255,
                //            grbit = CreateIndexGrbit.IndexDisallowNull,
                //            szIndexName = "IX_PreviousBlockHash",
                //            szKey = "+PreviousBlockHash\0\0",
                //            cbKey = "+PreviousBlockHash\0\0".Length
                //        }
                //    }, 1);

                //Api.JetCreateIndex2(jetSession, blockHeadersTableId,
                //    new JET_INDEXCREATE[]
                //    {
                //        new JET_INDEXCREATE
                //        {
                //            cbKeyMost = 255,
                //            grbit = CreateIndexGrbit.IndexDisallowNull,
                //            szIndexName = "IX_Height",
                //            szKey = "+Height\0\0",
                //            cbKey = "+Height\0\0".Length
                //        }
                //    }, 1);

                Api.JetCreateIndex2(jetSession, blockHeadersTableId,
                    new JET_INDEXCREATE[]
                    {
                        new JET_INDEXCREATE
                        {
                            cbKeyMost = 255,
                            grbit = CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_TotalWork",
                            szKey = "-TotalWork\0\0",
                            cbKey = "-TotalWork\0\0".Length
                        }
                    }, 1);

                Api.JetCloseTable(jetSession, blockHeadersTableId);
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
                        this.FreeCursor(cursor);
                    }
                    catch (Exception)
                    {
                        Api.JetCloseDatabase(jetSession, blockDbId, CloseDatabaseGrbit.None);
                        throw;
                    }
                }
                catch (Exception)
                {
                    Api.JetDetachDatabase(jetSession, jetDatabase);
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

        private BlockCursor OpenCursor()
        {
            BlockCursor cursor = null;

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
                cursor = new BlockCursor(this.jetDatabase, this.jetInstance);

            return cursor;
        }

        private void FreeCursor(BlockCursor cursor)
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
