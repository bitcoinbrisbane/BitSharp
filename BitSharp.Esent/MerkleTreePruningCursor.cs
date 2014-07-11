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
    internal class MerkleTreePruningCursor : IMerkleTreePruningCursor, IDisposable
    {
        private readonly UInt256 blockHash;
        private readonly BlockTxesCursor cursor;
        private readonly Action disposeAction;

        public MerkleTreePruningCursor(UInt256 blockHash, BlockTxesCursor cursor, Action disposeAction)
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

        public bool TryMoveToIndex(int index, out MerkleTreeNode node)
        {
            Api.MakeKey(cursor.jetSession, cursor.blocksTableId, this.blockHash.ToByteArray(), MakeKeyGrbit.NewKey);
            Api.MakeKey(cursor.jetSession, cursor.blocksTableId, index, MakeKeyGrbit.None);

            if (Api.TrySeek(cursor.jetSession, cursor.blocksTableId, SeekGrbit.SeekEQ))
            {
                node = ReadNode();
                return true;
            }
            else
            {
                node = default(MerkleTreeNode);
                return false;
            }
        }

        public bool TryMoveLeft(out MerkleTreeNode node)
        {
            if (Api.TryMovePrevious(cursor.jetSession, cursor.blocksTableId)
                && this.blockHash == new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId)))
            {
                node = ReadNode();
                return true;
            }
            else
            {
                node = default(MerkleTreeNode);
                return false;
            }
        }

        public bool TryMoveRight(out MerkleTreeNode node)
        {
            if (Api.TryMoveNext(cursor.jetSession, cursor.blocksTableId)
                && this.blockHash == new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockHashColumnId)))
            {
                node = ReadNode();
                return true;
            }
            else
            {
                MoveLeft();
                node = default(MerkleTreeNode);
                return false;
            }
        }

        public void WriteNode(MerkleTreeNode node)
        {
            if (!node.Pruned)
                throw new ArgumentException();

            Api.JetPrepareUpdate(cursor.jetSession, cursor.blocksTableId, JET_prep.Replace);
            try
            {
                Debug.Assert(node.Index == Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockTxIndexColumnId).Value);

                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockDepthColumnId, node.Depth);
                Api.SetColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxHashColumnId, node.Hash.ToByteArray());
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
            MerkleTreeNode node;
            if (!this.TryMoveLeft(out node))
                throw new InvalidOperationException();
        }

        public void DeleteNodeToRight()
        {
            MerkleTreeNode node;
            if (!this.TryMoveRight(out node))
                throw new InvalidOperationException();

            Api.JetDelete(cursor.jetSession, cursor.blocksTableId);

            MoveLeft();
        }

        private MerkleTreeNode ReadNode()
        {
            var index = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockTxIndexColumnId).Value;
            var depth = Api.RetrieveColumnAsInt32(cursor.jetSession, cursor.blocksTableId, cursor.blockDepthColumnId).Value;
            var txHash = new UInt256(Api.RetrieveColumn(cursor.jetSession, cursor.blocksTableId, cursor.blockTxHashColumnId));

            var pruned = depth >= 0;
            depth = Math.Max(0, depth);

            return new MerkleTreeNode(index, depth, txHash, pruned);
        }
    }
}
