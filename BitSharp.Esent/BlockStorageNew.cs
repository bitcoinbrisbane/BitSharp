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

namespace BitSharp.Esent
{
    public class BlockStorageNew : IBlockStorageNew
    {
        private readonly SemaphoreSlim semaphore;

        private readonly string jetDirectory;
        private readonly string jetDatabase;
        private readonly Instance jetInstance;
        private Session jetSession;
        private bool inTransaction;

        private readonly JET_DBID blockDbId;

        private readonly JET_TABLEID blockHeadersTableId;
        private readonly JET_COLUMNID blockHeaderHashColumnId;
        private readonly JET_COLUMNID blockHeaderBytesColumnId;

        private readonly JET_TABLEID blocksTableId;
        private readonly JET_COLUMNID blockHashColumnId;
        private readonly JET_COLUMNID blockTxIndexColumnId;
        private readonly JET_COLUMNID blockTxHashColumnId;
        private readonly JET_COLUMNID blockTxBytesColumnId;

        private readonly ConcurrentSetBuilder<UInt256> missingData;

        public BlockStorageNew(string baseDirectory)
        {
            this.missingData = new ConcurrentSetBuilder<UInt256>();

            this.semaphore = new SemaphoreSlim(1);

            this.jetDirectory = Path.Combine(baseDirectory, "BlocksNew");
            this.jetDatabase = Path.Combine(this.jetDirectory, "BlocksNew.edb");

            //TODO currently configured by PersistentDictionary
            //SystemParameters.DatabasePageSize = 8192;
            //var maxDbSizeInPages = 500.MILLION() / SystemParameters.DatabasePageSize;

            this.jetInstance = CreateInstance(this.jetDirectory);
            this.jetInstance.Init();

            if (!File.Exists(this.jetDatabase))
                CreateDatabase(this.jetDatabase, this.jetInstance);

            OpenDatabase(this.jetDatabase, this.jetInstance, false /*readOnly*/,
                out this.jetSession,
                out this.blockDbId,
                out this.blockHeadersTableId,
                    out this.blockHeaderHashColumnId,
                    out this.blockHeaderBytesColumnId,
                out this.blocksTableId,
                    out this.blockHashColumnId,
                    out this.blockTxIndexColumnId,
                    out this.blockTxHashColumnId,
                    out this.blockTxBytesColumnId);
        }

        public void Dispose()
        {
            this.jetSession.Dispose();
            this.jetInstance.Dispose();
            this.semaphore.Dispose();
        }

        public event Action<UInt256, Block> OnAddition;

        public event Action<UInt256, Block> OnModification;

        public event Action<UInt256> OnRemoved;

        public bool ContainsBlock(UInt256 blockHash)
        {
            return this.semaphore.Do(() =>
            {
                Api.JetSetCurrentIndex(this.jetSession, this.blockHeadersTableId, "IX_BlockHash");
                Api.MakeKey(this.jetSession, this.blockHeadersTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                return Api.TrySeek(this.jetSession, this.blockHeadersTableId, SeekGrbit.SeekEQ);
            });
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
            this.semaphore.Do(() =>
            {
                Api.JetBeginTransaction(this.jetSession);
                try
                {
                    AddBlockHeader(block.Header);
                    for (var txIndex = 0; txIndex < block.Transactions.Length; txIndex++)
                    {
                        var tx = block.Transactions[txIndex];
                        AddTransaction(block.Hash, txIndex, tx.Hash, DataEncoder.EncodeTransaction(tx));
                    }

                    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                }
                catch (Exception)
                {
                    Api.JetRollback(this.jetSession, RollbackTransactionGrbit.None);
                    throw;
                }
            });

            this.missingData.Remove(block.Hash);
            RaiseOnAddition(block.Hash, block);
        }

        private void AddBlockHeader(BlockHeader blockHeader)
        {
            Api.JetPrepareUpdate(this.jetSession, this.blockHeadersTableId, JET_prep.Insert);
            try
            {
                Api.SetColumn(this.jetSession, this.blockHeadersTableId, this.blockHeaderHashColumnId, blockHeader.Hash.ToByteArray());
                Api.SetColumn(this.jetSession, this.blockHeadersTableId, this.blockHeaderBytesColumnId, DataEncoder.EncodeBlockHeader(blockHeader));

                Api.JetUpdate(this.jetSession, this.blockHeadersTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(this.jetSession, this.blockHeadersTableId, JET_prep.Cancel);
                throw;
            }
        }

        private void AddTransaction(UInt256 blockHash, int txIndex, UInt256 txHash, byte[] txBytes)
        {
            Api.JetPrepareUpdate(this.jetSession, this.blocksTableId, JET_prep.Insert);
            try
            {
                Api.SetColumn(this.jetSession, this.blocksTableId, this.blockHashColumnId, blockHash.ToByteArray());
                Api.SetColumn(this.jetSession, this.blocksTableId, this.blockTxIndexColumnId, txIndex);
                Api.SetColumn(this.jetSession, this.blocksTableId, this.blockTxHashColumnId, txHash.ToByteArray());
                Api.SetColumn(this.jetSession, this.blocksTableId, this.blockTxBytesColumnId, txBytes);

                Api.JetUpdate(this.jetSession, this.blocksTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(this.jetSession, this.blocksTableId, JET_prep.Cancel);
                throw;
            }
        }

        public bool TryGetBlock(UInt256 blockHash, out Block block)
        {
            Block result = null;
            var found = this.semaphore.Do(() =>
            {
                Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
                try
                {
                    Api.JetSetCurrentIndex(this.jetSession, this.blockHeadersTableId, "IX_BlockHash");
                    Api.MakeKey(this.jetSession, this.blockHeadersTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                    if (Api.TrySeek(this.jetSession, this.blockHeadersTableId, SeekGrbit.SeekEQ))
                    {
                        var blockHeader = DataEncoder.DecodeBlockHeader(Api.RetrieveColumn(this.jetSession, this.blockHeadersTableId, this.blockHeaderBytesColumnId));

                        var transactions = new List<BitSharp.Core.Domain.Transaction>();

                        Api.JetSetCurrentIndex(this.jetSession, this.blocksTableId, "IX_BlockHashTxIndex");
                        Api.MakeKey(this.jetSession, this.blocksTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                        Api.MakeKey(this.jetSession, this.blocksTableId, -1, MakeKeyGrbit.None);
                        if (Api.TrySeek(this.jetSession, this.blocksTableId, SeekGrbit.SeekGE))
                        {
                            var found2 = false;
                            do
                            {
                                if (blockHash == new UInt256(Api.RetrieveColumn(this.jetSession, this.blocksTableId, this.blockHashColumnId)))
                                {
                                    found2 = true;
                                    transactions.Add(DataEncoder.DecodeTransaction(Api.RetrieveColumn(this.jetSession, this.blocksTableId, this.blockTxBytesColumnId)));
                                }
                                else
                                {
                                    break;
                                }
                            } while (Api.TryMoveNext(this.jetSession, this.blocksTableId));

                            if (found2)
                            {
                                result = new Block(blockHeader, ImmutableArray.Create(transactions.ToArray()));
                                //var merkleRoot = DataCalculator.CalculateMerkleRoot(result.Transactions.Select(x => x.Hash).ToImmutableList());
                                //if (merkleRoot != blockHeader.MerkleRoot)
                                //{
                                //    Debugger.Break();
                                //}
                            }

                            return found2;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                finally
                {
                    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                }
            });

            if (found)
                block = result;
            else
                block = default(Block);

            if (found)
            {
                if (this.missingData.Remove(blockHash))
                    RaiseOnAddition(blockHash, block);
            }
            else
            {
                if (this.missingData.Add(blockHash))
                    RaiseOnMissing(blockHash);
            }

            return found;
        }

        public bool TryGetTransaction(UInt256 blockHash, int txIndex, out BitSharp.Core.Domain.Transaction transaction)
        {
            BitSharp.Core.Domain.Transaction result = null;
            var found = this.semaphore.Do(() =>
            {
                Api.JetBeginTransaction2(this.jetSession, BeginTransactionGrbit.ReadOnly);
                try
                {
                    Api.JetSetCurrentIndex(this.jetSession, this.blocksTableId, "IX_BlockHashTxIndex");
                    Api.MakeKey(this.jetSession, this.blocksTableId, blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
                    Api.MakeKey(this.jetSession, this.blocksTableId, txIndex, MakeKeyGrbit.None);
                    if (Api.TrySeek(this.jetSession, this.blocksTableId, SeekGrbit.SeekEQ))
                    {
                        result = DataEncoder.DecodeTransaction(Api.RetrieveColumn(this.jetSession, this.blocksTableId, this.blockTxBytesColumnId));
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                finally
                {
                    Api.JetCommitTransaction(this.jetSession, CommitTransactionGrbit.LazyFlush);
                }
            });

            if (found)
                transaction = result;
            else
                transaction = default(BitSharp.Core.Domain.Transaction);

            return found;
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

        private static void CreateDatabase(string jetDatabase, Instance jetInstance)
        {
            JET_DBID blockDbId;
            JET_TABLEID blockHeadersTableId;
            JET_COLUMNID blockHeaderHashColumnId;
            JET_COLUMNID blockHeaderBytesColumnId;
            JET_TABLEID blocksTableId;
            JET_COLUMNID blockHashColumnId;
            JET_COLUMNID blockTxIndexColumnId;
            JET_COLUMNID blockTxHashColumnId;
            JET_COLUMNID blockTxBytesColumnId;

            using (var jetSession = new Session(jetInstance))
            {
                Api.JetCreateDatabase(jetSession, jetDatabase, "", out blockDbId, CreateDatabaseGrbit.None);

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

        private static void OpenDatabase(string jetDatabase, Instance jetInstance, bool readOnly,
            out Session jetSession,
            out JET_DBID blockDbId,
            out JET_TABLEID blockHeadersTableId,
            out JET_COLUMNID blockHeaderHashColumnId,
            out JET_COLUMNID blockHeaderBytesColumnId,
            out JET_TABLEID blocksTableId,
            out JET_COLUMNID blockHashColumnId,
            out JET_COLUMNID blockTxIndexColumnId,
            out JET_COLUMNID blockTxHashColumnId,
            out JET_COLUMNID blockTxBytesColumnId)
        {
            jetSession = new Session(jetInstance);

            Api.JetAttachDatabase(jetSession, jetDatabase, readOnly ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None);
            Api.JetOpenDatabase(jetSession, jetDatabase, "", out blockDbId, readOnly ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None);

            Api.JetOpenTable(jetSession, blockDbId, "BlockHeaders", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out blockHeadersTableId);
            blockHeaderHashColumnId = Api.GetTableColumnid(jetSession, blockHeadersTableId, "BlockHash");
            blockHeaderBytesColumnId = Api.GetTableColumnid(jetSession, blockHeadersTableId, "BlockHeaderBytes");

            Api.JetOpenTable(jetSession, blockDbId, "Blocks", null, 0, readOnly ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None, out blocksTableId);
            blockHashColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "BlockHash");
            blockTxIndexColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "TxIndex");
            blockTxHashColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "TxHash");
            blockTxBytesColumnId = Api.GetTableColumnid(jetSession, blocksTableId, "TxBytes");
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

        public event Action<UInt256> OnMissing;

        public string Name
        {
            get { return "Blocks"; }
        }

        public ImmutableHashSet<UInt256> MissingData
        {
            get { return this.missingData.ToImmutable(); }
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
                    if (this.missingData.Add(blockHash))
                        RaiseOnMissing(blockHash);

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
            throw new NotImplementedException();
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
    }
}
