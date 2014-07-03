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

namespace BitSharp.Esent
{
    public class BlockStorageNew : IBlockStorageNew
    {
        private readonly string jetDirectory;
        private readonly string jetDatabase;
        private readonly Instance jetInstance;
        private bool inTransaction;

        private readonly BlockStorageCursor[] cursors;
        private readonly object cursorsLock;

        //TODO track these more cleanly, notPresentBlocks is so that ContainsBlock doesn't raise missing events
        private readonly ConcurrentSetBuilder<UInt256> presentBlocks;
        private readonly ConcurrentSetBuilder<UInt256> notPresentBlocks;
        private readonly ConcurrentSetBuilder<UInt256> missingBlocks;

        public BlockStorageNew(string baseDirectory)
        {
            this.presentBlocks = new ConcurrentSetBuilder<UInt256>();
            this.notPresentBlocks = new ConcurrentSetBuilder<UInt256>();
            this.missingBlocks = new ConcurrentSetBuilder<UInt256>();

            this.jetDirectory = Path.Combine(baseDirectory, "BlocksNew");
            this.jetDatabase = Path.Combine(this.jetDirectory, "BlocksNew.edb");

            //TODO currently configured by PersistentDictionary
            //SystemParameters.DatabasePageSize = 8192;
            //var maxDbSizeInPages = 500.MILLION() / SystemParameters.DatabasePageSize;

            this.cursors = new BlockStorageCursor[64];
            this.cursorsLock = new object();

            this.jetInstance = CreateInstance(this.jetDirectory);
            this.jetInstance.Init();
            try
            {
                this.CreateOrOpenDatabase(this.jetDirectory, this.jetDatabase, this.jetInstance);
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

        public event Action<UInt256, Block> OnAddition;

        public event Action<UInt256, Block> OnModification;

        public event Action<UInt256> OnRemoved;

        public event Action<UInt256> OnMissing;

        public bool ContainsBlock(UInt256 blockHash)
        {
            if (this.presentBlocks.Contains(blockHash))
                return true;
            else if (this.notPresentBlocks.Contains(blockHash))
                return false;
            else if (this.missingBlocks.Contains(blockHash))
                return false;

            var cursor = this.OpenCursor();
            try
            {
                Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockHeadersTableId, "IX_BlockHash");
                Api.MakeKey(cursor.jetSession, cursor.blockHeadersTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                var found = Api.TrySeek(cursor.jetSession, cursor.blockHeadersTableId, SeekGrbit.SeekEQ);

                if (found)
                {
                    if (this.presentBlocks.Add(blockHash))
                        RaiseOnAddition(blockHash, null);
                }
                else
                {
                    this.notPresentBlocks.Add(blockHash);
                }

                return found;
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        //public bool TryGetTransaction(UInt256 blockHash, int txIndex, out UnspentTx unspentTx)
        //{
        //    Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
        //    try
        //    {
        //        //Api.JetSetCurrentIndex(this.jetSession, this.unspentTxTableId, "IX_TxHash");
        //        Api.MakeKey(this.jetSession, this.unspentTxTableId, txHash.ToByteArray(), MakeKeyGrbit.NewKey);
        //        if (Api.TrySeek(this.jetSession, this.unspentTxTableId, SeekGrbit.SeekEQ))
        //        {
        //            var confirmedBlockHash = new UInt256(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.confirmedBlockHashColumnId));
        //            var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(this.jetSession, this.unspentTxTableId, this.outputStatesColumnId));

        //            unspentTx = new UnspentTx(confirmedBlockHash, outputStates);
        //            return true;
        //        }
        //        else
        //        {
        //            unspentTx = default(UnspentTx);
        //            return false;
        //        }
        //    }
        //    finally
        //    {
        //        Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
        //    }
        //}

        public void AddBlock(Block block)
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetBeginTransaction(cursor.jetSession);
                try
                {
                    AddBlockHeader(block.Header, cursor);
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

            this.missingBlocks.Remove(block.Hash);
            this.notPresentBlocks.Remove(block.Hash);
            if (this.presentBlocks.Add(block.Hash))
                RaiseOnAddition(block.Hash, block);
        }

        private void AddBlockHeader(BlockHeader blockHeader, BlockStorageCursor cursor)
        {
            Api.JetPrepareUpdate(cursor.jetSession, cursor.blockHeadersTableId, JET_prep.Insert);
            try
            {
                Api.SetColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderHashColumnId, blockHeader.Hash.ToByteArray());
                Api.SetColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderBytesColumnId, DataEncoder.EncodeBlockHeader(blockHeader));

                Api.JetUpdate(cursor.jetSession, cursor.blockHeadersTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(cursor.jetSession, cursor.blockHeadersTableId, JET_prep.Cancel);
                throw;
            }
        }

        private void AddTransaction(UInt256 blockHash, int txIndex, UInt256 txHash, byte[] txBytes, BlockStorageCursor cursor)
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

        public bool TryGetBlock(UInt256 blockHash, out Block block)
        {
            var cursor = this.OpenCursor();
            try
            {
                Api.JetBeginTransaction2(cursor.jetSession, BeginTransactionGrbit.ReadOnly);
                try
                {
                    Api.JetSetCurrentIndex(cursor.jetSession, cursor.blockHeadersTableId, "IX_BlockHash");
                    Api.MakeKey(cursor.jetSession, cursor.blockHeadersTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                    if (Api.TrySeek(cursor.jetSession, cursor.blockHeadersTableId, SeekGrbit.SeekEQ))
                    {
                        var blockHeader = DataEncoder.DecodeBlockHeader(Api.RetrieveColumn(cursor.jetSession, cursor.blockHeadersTableId, cursor.blockHeaderBytesColumnId));

                        var transactions = ImmutableArray.CreateBuilder<BitSharp.Core.Domain.Transaction>();

                        Api.JetSetCurrentIndex(cursor.jetSession, cursor.blocksTableId, "IX_BlockHashTxIndex");
                        Api.MakeKey(cursor.jetSession, cursor.blocksTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                        Api.MakeKey(cursor.jetSession, cursor.blocksTableId, -1, MakeKeyGrbit.None);
                        if (Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekGE))
                        {
                            do
                            {
                                if (blockHash != new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId)))
                                    break;

                                transactions.Add(DataEncoder.DecodeTransaction(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxBytesColumnId)));
                            } while (Api.TryMoveNext(cursor.jetSession, cursor.blocksTableId));

                            if (transactions.Count > 0)
                            {
                                block = new Block(blockHeader, transactions.ToImmutable());

                                this.missingBlocks.Remove(blockHash);
                                this.notPresentBlocks.Remove(block.Hash); 
                                if (this.presentBlocks.Add(blockHash))
                                    RaiseOnAddition(blockHash, block);

                                return true;
                            }
                            else
                            {
                                block = default(Block);

                                this.presentBlocks.Remove(blockHash);
                                if (this.missingBlocks.Add(blockHash))
                                    RaiseOnMissing(blockHash);

                                return false;
                            }
                        }
                        else
                        {
                            block = default(Block);

                            this.presentBlocks.Remove(blockHash);
                            if (this.missingBlocks.Add(blockHash))
                                RaiseOnMissing(blockHash);

                            return false;
                        }
                    }
                    else
                    {
                        block = default(Block);

                        this.presentBlocks.Remove(blockHash);
                        if (this.missingBlocks.Add(blockHash))
                            RaiseOnMissing(blockHash);

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

        public IEnumerable<BlockTx> ReadBlock(ChainedHeader chainedHeader)
        {
            return DataCalculatorNew.ReadBlockElements(chainedHeader.Hash, chainedHeader.MerkleRoot, this.ReadBlockTransactions(chainedHeader.Hash));
        }

        private IEnumerable<BlockTx> ReadBlockTransactions(UInt256 blockHash)
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

                            // missing data if any transactions are pruned
                            if (depth >= 0)
                                throw new MissingDataException(blockHash);

                            // verify transaction is not corrupt
                            if (txHash != new UInt256(sha256.ComputeDoubleHash(txBytes)))
                                throw new MissingDataException(blockHash);

                            var tx = DataEncoder.DecodeTransaction(txBytes);
                            var blockTx = new BlockTx(blockHash, txIndex, 0 /*depth*/, txHash, tx);

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
            instance.Parameters.MaxTemporaryTables = 0;
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

        private void CreateOrOpenDatabase(string jetDirectory, string jetDatabase, Instance jetInstance)
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

                CreateDatabase(jetDatabase, jetInstance);
            }
        }


        private void CreateDatabase(string jetDatabase, Instance jetInstance)
        {
            JET_DBID blockDbId;
            JET_TABLEID globalsTableId;
            JET_COLUMNID flushColumnId;
            JET_TABLEID blockHeadersTableId;
            JET_COLUMNID blockHeaderHashColumnId;
            JET_COLUMNID blockHeaderBytesColumnId;
            JET_TABLEID blocksTableId;
            JET_COLUMNID blockHashColumnId;
            JET_COLUMNID blockTxIndexColumnId;
            JET_COLUMNID blockDepthColumnId;
            JET_COLUMNID blockTxHashColumnId;
            JET_COLUMNID blockTxBytesColumnId;

            using (var jetSession = new Session(jetInstance))
            {
                Api.JetCreateDatabase(jetSession, jetDatabase, "", out blockDbId, CreateDatabaseGrbit.None);

                var defaultValue = BitConverter.GetBytes(0);
                Api.JetCreateTable(jetSession, blockDbId, "global", 0, 0, out globalsTableId);
                Api.JetAddColumn(jetSession, globalsTableId, "Flush", new JET_COLUMNDEF { coltyp = JET_coltyp.Long, grbit = ColumndefGrbit.ColumnEscrowUpdate }, defaultValue, defaultValue.Length, out flushColumnId);
                Api.JetCloseTable(jetSession, globalsTableId);

                Api.JetCreateTable(jetSession, blockDbId, "BlockHeaders", 0, 0, out blockHeadersTableId);
                Api.JetAddColumn(jetSession, blockHeadersTableId, "BlockHash", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 32, grbit = ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnFixed }, null, 0, out blockHeaderHashColumnId);
                Api.JetAddColumn(jetSession, blockHeadersTableId, "BlockHeaderBytes", new JET_COLUMNDEF { coltyp = JET_coltyp.Binary, cbMax = 80 }, null, 0, out blockHeaderBytesColumnId);

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

                Api.JetCloseTable(jetSession, blockHeadersTableId);

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
                            grbit = CreateIndexGrbit.IndexPrimary | CreateIndexGrbit.IndexDisallowNull,
                            szIndexName = "IX_BlockHashTxIndex",
                            szKey = "+BlockHash\0+TxIndex\0\0",
                            cbKey = "+BlockHash\0+TxIndex\0\0".Length
                        }
                    }, 1);

                Api.JetCloseTable(jetSession, blocksTableId);
            }
        }

        private void OpenDatabase(string jetDatabase, Instance jetInstance)
        {
            JET_DBID blockDbId;

            var readOnly = false;

            using (var jetSession = new Session(jetInstance))
            {
                Api.JetAttachDatabase(jetSession, jetDatabase, readOnly ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None);
                try
                {
                    Api.JetOpenDatabase(jetSession, jetDatabase, "", out blockDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);
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
                    Api.JetDetachDatabase(jetSession, jetDatabase);
                    throw;
                }
            }
        }

        public int Count
        {
            get { return 0; }
        }

        public IEnumerable<UInt256> Keys
        {
            get { throw new NotImplementedException(); }
        }

        public IEnumerable<Block> Values
        {
            get { throw new NotImplementedException(); }
        }

        public string Name
        {
            get { return "Blocks"; }
        }

        public ImmutableHashSet<UInt256> MissingData
        {
            get { return this.missingBlocks.ToImmutable(); }
        }

        public bool ContainsKey(UInt256 blockHash)
        {
            return ContainsBlock(blockHash);
        }

        public bool TryGetValue(UInt256 blockHash, out Block block)
        {
            return TryGetBlock(blockHash, out block);
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

        public Block this[UInt256 blockHash]
        {
            get
            {
                Block block;
                if (TryGetBlock(blockHash, out block))
                {
                    return block;
                }
                else
                {
                    throw new MissingDataException(blockHash);
                }
            }
            set
            {
                TryAdd(blockHash, value);
            }
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

        private void RaiseOnAddition(UInt256 blockHash, Block block)
        {
            var handler = this.OnAddition;
            if (handler != null)
                handler(blockHash, block);
        }

        private void RaiseOnModification(UInt256 blockHash, Block block)
        {
            var handler = this.OnModification;
            if (handler != null)
                handler(blockHash, block);
        }

        private void RaiseOnRemoved(UInt256 blockHash)
        {
            var handler = this.OnRemoved;
            if (handler != null)
                handler(blockHash);
        }

        private void RaiseOnMissing(UInt256 blockHash)
        {
            var handler = this.OnMissing;
            if (handler != null)
                handler(blockHash);
        }

        private BlockStorageCursor OpenCursor()
        {
            BlockStorageCursor cursor = null;

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
                cursor = new BlockStorageCursor(this.jetDatabase, this.jetInstance);

            return cursor;
        }

        private void FreeCursor(BlockStorageCursor cursor)
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
