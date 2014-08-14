using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    internal class ChainState : IChainState
    {
        private readonly Chain chain;

        private readonly IChainStateCursor[] cursors;
        private readonly object cursorsLock = new object();

        public ChainState(Chain chain, IStorageManager storageManager)
        {
            this.chain = chain;

            // create a cache of cursors that are in an open snapshot transaction with the current chain state
            this.cursors = new IChainStateCursor[16];
            try
            {
                for (var i = 0; i < this.cursors.Length; i++)
                {
                    // open the cursor and begin a transaction
                    this.cursors[i] = storageManager.OpenChainStateCursor();
                    this.cursors[i].BeginTransaction();

                    // verify the chain state matches the expected chain
                    if (chain.LastBlock != this.cursors[i].GetChainTip())
                        throw new InvalidOperationException();
                }
            }
            catch (Exception)
            {
                // ensure any opened cursors are cleaned up on an error
                this.cursors.DisposeList();
                throw;
            }
        }

        ~ChainState()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            this.cursors.DisposeList();
        }

        public Chain Chain
        {
            get { return this.chain; }
        }

        public int UnspentTxCount
        {
            get
            {
                var cursor = this.OpenCursor();
                try
                {
                    return cursor.UnspentTxCount;
                }
                finally
                {
                    this.FreeCursor(cursor);
                }
            }
        }

        public bool ContainsUnspentTx(UInt256 txHash)
        {
            var cursor = this.OpenCursor();
            try
            {
                return cursor.ContainsUnspentTx(txHash);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public bool TryGetUnspentTx(UInt256 txHash, out UnspentTx unspentTx)
        {
            var cursor = this.OpenCursor();
            try
            {
                return cursor.TryGetUnspentTx(txHash, out unspentTx);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public IEnumerable<UnspentTx> ReadUnspentTransactions()
        {
            var cursor = this.OpenCursor();
            try
            {
                foreach (var unspentTx in cursor.ReadUnspentTransactions())
                    yield return unspentTx;
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public bool ContainsBlockSpentTxes(int blockIndex)
        {
            var cursor = this.OpenCursor();
            try
            {
                return cursor.ContainsBlockSpentTxes(blockIndex);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        public bool TryGetBlockSpentTxes(int blockIndex, out IImmutableList<SpentTx> spentTxes)
        {
            var cursor = this.OpenCursor();
            try
            {
                return cursor.TryGetBlockSpentTxes(blockIndex, out spentTxes);
            }
            finally
            {
                this.FreeCursor(cursor);
            }
        }

        private IChainStateCursor OpenCursor()
        {
            IChainStateCursor cursor = null;

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

            if (cursor != null)
                return cursor;
            else
                //TODO better error when cursors run out
                //TODO there should be the ability to wait and timeout
                throw new InvalidOperationException();
        }

        private void FreeCursor(IChainStateCursor cursor)
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
            {
                cursor.Dispose();
                Debugger.Break();
                throw new InvalidOperationException();
            }
        }
    }
}
