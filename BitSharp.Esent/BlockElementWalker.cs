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
    internal class BlockElementWalker : IBlockElementWalker, IDisposable
    {
        private readonly UInt256 blockHash;
        private readonly BlockTxesCursor cursor;
        private readonly Action disposeAction;

        public BlockElementWalker(UInt256 blockHash, BlockTxesCursor cursor, Action disposeAction)
        {
            this.blockHash = blockHash;
            this.cursor = cursor;
            this.disposeAction = disposeAction;

        }

        public void Dispose()
        {
            this.disposeAction();
        }

        public void BeginTransaction()
        {
            Api.JetBeginTransaction2(cursor.jetSession, BeginTransactionGrbit.None);
            Api.JetSetCurrentIndex(cursor.jetSession, cursor.blocksTableId, "IX_BlockHashTxIndex");
        }

        public void CommitTransaction()
        {
            Api.JetCommitTransaction(cursor.jetSession, CommitTransactionGrbit.LazyFlush);
        }

        public bool TryMoveToIndex(int index, out BlockElement element)
        {
            Api.MakeKey(cursor.jetSession, cursor.blocksTableId, this.blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
            Api.MakeKey(cursor.jetSession, cursor.blocksTableId, index, MakeKeyGrbit.None);

            if (Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekEQ))
            {
                element = ReadElement();
                return true;
            }
            else
            {
                element = default(BlockElement);
                return false;
            }
        }

        public bool TryMoveLeft(out BlockElement element)
        {
            if (Api.TryMovePrevious(cursor.jetSession, cursor.blocksTableId)
                && this.blockHash == new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId)))
            {
                element = ReadElement();
                return true;
            }
            else
            {
                element = default(BlockElement);
                return false;
            }
        }

        public bool TryMoveRight(out BlockElement element)
        {
            if (Api.TryMoveNext(cursor.jetSession, cursor.blocksTableId)
                && this.blockHash == new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId)))
            {
                element = ReadElement();
                return true;
            }
            else
            {
                MoveLeft();
                element = default(BlockElement);
                return false;
            }
        }

        public void WriteElement(BlockElement element)
        {
            Api.JetPrepareUpdate(cursor.jetSession, cursor.blocksTableId, JET_prep.Replace);
            try
            {
                Debug.Assert(element.Index == Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockTxIndexColumnId).Value);

                //TODO i'm using -1 depth to mean not pruned, this should be interpreted as depth 0
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockDepthColumnId, !element.Pruned ? -1 : element.Depth);
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxHashColumnId, element.Hash.ToByteArray());
                if (element.Pruned)
                    Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxBytesColumnId, null);

                Api.JetUpdate(cursor.jetSession, cursor.blocksTableId);
            }
            catch (Exception)
            {
                Api.JetPrepareUpdate(cursor.jetSession, cursor.blocksTableId, JET_prep.Cancel);
                throw;
            }
        }

        public void MoveLeft()
        {
            BlockElement element;
            if (!this.TryMoveLeft(out element))
                throw new InvalidOperationException();
        }

        public void DeleteElementToRight()
        {
            var origElement = ReadElement();

            BlockElement element;
            if (!this.TryMoveRight(out element))
                throw new InvalidOperationException();

            Api.JetDelete(cursor.jetSession, cursor.blocksTableId);

            MoveLeft();
            var newElement = ReadElement();
            Debug.Assert(origElement == newElement);
        }

        private BlockElement ReadElement()
        {
            var index = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockTxIndexColumnId).Value;
            var depth = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockDepthColumnId).Value;
            var txHash = new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxHashColumnId));
            var pruned = depth >= 0;

            return new BlockElement(index, Math.Max(0, depth), txHash, pruned);
        }
    }
}
