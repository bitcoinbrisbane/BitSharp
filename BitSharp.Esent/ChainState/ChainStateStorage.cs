using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Builders;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    public class ChainStateStorage : IChainStateStorage
    {
        private readonly string jetDatabase;
        private readonly Instance jetInstance;

        private readonly ChainStateStorageCursor[] cursors;
        private readonly object cursorsLock;

        internal ChainStateStorage(string jetDatabase, Instance jetInstance)
        {
            this.jetDatabase = jetDatabase;
            this.jetInstance = jetInstance;

            this.cursors = new ChainStateStorageCursor[32];
            this.cursorsLock = new object();

            try
            {
                for (var i = 0; i < this.cursors.Length; i++)
                {
                    this.cursors[i] = new ChainStateStorageCursor(this.jetDatabase, this.jetInstance, readOnly: true);
                    Api.JetBeginTransaction2(this.cursors[i].jetSession, BeginTransactionGrbit.ReadOnly);
                }
            }
            catch (Exception)
            {
                this.cursors.DisposeList();
            }
        }

        ~ChainStateStorage()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            this.cursors.DisposeList();
        }

        public IEnumerable<ChainedHeader> ReadChain()
        {
            var cursor = this.OpenCursor();
            try
            {
                return ChainStateStorage.ReadChain(cursor);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public int TransactionCount
        {
            //TODO
            get { return 0; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                return ContainsTransaction(cursor, txHash);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx)
        {
            var cursor = this.OpenCursor();
            try
            {
                return TryGetTransaction(cursor, txHash, out unspentTx);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> ReadUnspentTransactions()
        {
            var cursor = this.OpenCursor();
            try
            {
                return ReadUnspentTransactions(cursor);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        private ChainStateStorageCursor OpenCursor()
        {
            ChainStateStorageCursor cursor = null;

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
                throw new InvalidOperationException();

            return cursor;
        }

        private void FreeCursor(ChainStateStorageCursor cursor)
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

            Debug.Assert(cached);
        }

        internal static bool ContainsTransaction(ChainStateStorageCursor cursor, UInt256 txHash)
        {
            Api.JetSetCurrentIndex(cursor.jetSession, cursor.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(cursor.jetSession, cursor.unspentTxTableId, DbEncoder.EncodeUInt256(txHash), MakeKeyGrbit.NewKey);
            return Api.TrySeek(cursor.jetSession, cursor.unspentTxTableId, SeekGrbit.SeekEQ);
        }

        internal static bool TryGetTransaction(ChainStateStorageCursor cursor, UInt256 txHash, out UnspentTx unspentTx)
        {
            Api.JetSetCurrentIndex(cursor.jetSession, cursor.unspentTxTableId, "IX_TxHash");
            Api.MakeKey(cursor.jetSession, cursor.unspentTxTableId, DbEncoder.EncodeUInt256(txHash), MakeKeyGrbit.NewKey);
            if (Api.TrySeek(cursor.jetSession, cursor.unspentTxTableId, SeekGrbit.SeekEQ))
            {
                var blockIndex = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.unspentTxTableId, cursor.blockIndexColumnId).Value;
                var txIndex = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.unspentTxTableId, cursor.txIndexColumnId).Value;
                var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(cursor.jetSession, cursor.unspentTxTableId, cursor.outputStatesColumnId));

                unspentTx = new UnspentTx(blockIndex, txIndex, outputStates);
                return true;
            }

            unspentTx = default(UnspentTx);
            return false;
        }

        internal static IEnumerable<KeyValuePair<UInt256, UnspentTx>> ReadUnspentTransactions(ChainStateStorageCursor cursor)
        {
            Api.JetSetCurrentIndex(cursor.jetSession, cursor.unspentTxTableId, "IX_TxHash");

            if (Api.TryMoveFirst(cursor.jetSession, cursor.unspentTxTableId))
            {
                do
                {
                    var txHash = DbEncoder.DecodeUInt256(Api.RetrieveColumn(cursor.jetSession, cursor.unspentTxTableId, cursor.txHashColumnId));
                    var blockIndex = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.unspentTxTableId, cursor.blockIndexColumnId).Value;
                    var txIndex = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.unspentTxTableId, cursor.txIndexColumnId).Value;
                    var outputStates = DataEncoder.DecodeOutputStates(Api.RetrieveColumn(cursor.jetSession, cursor.unspentTxTableId, cursor.outputStatesColumnId));

                    yield return new KeyValuePair<UInt256, UnspentTx>(txHash, new UnspentTx(blockIndex, txIndex, outputStates));
                } while (Api.TryMoveNext(cursor.jetSession, cursor.unspentTxTableId));
            }
        }

        internal static IEnumerable<ChainedHeader> ReadChain(ChainStateStorageCursor cursor)
        {
            var chainBuilder = new ChainBuilder();

            Api.JetSetCurrentIndex(cursor.jetSession, cursor.chainTableId, "IX_BlockHeight");

            if (Api.TryMoveFirst(cursor.jetSession, cursor.chainTableId))
            {
                do
                {
                    var chainedHeader = DataEncoder.DecodeChainedHeader(Api.RetrieveColumn(cursor.jetSession, cursor.chainTableId, cursor.chainedHeaderBytesColumnId));
                    yield return chainedHeader;
                } while (Api.TryMoveNext(cursor.jetSession, cursor.chainTableId));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
