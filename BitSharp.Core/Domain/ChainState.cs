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

        private readonly DisposableCache<DisposeHandle<IChainStateCursor>> cursorCache;

        public ChainState(Chain chain, IStorageManager storageManager)
        {
            this.chain = chain;

            // create a cache of cursors that are in an open snapshot transaction with the current chain state
            this.cursorCache = new DisposableCache<DisposeHandle<IChainStateCursor>>(16);
            try
            {
                for (var i = 0; i < this.cursorCache.Capacity; i++)
                {
                    // open the cursor
                    var handle = storageManager.OpenChainStateCursor();
                    var cursor = handle.Item;

                    // cache the cursor
                    // this must be done before beginning the transaction as caching will rollback any transactions
                    this.cursorCache.CacheItem(handle);

                    // begin transaction to take the snapshot
                    cursor.BeginTransaction();

                    // verify the chain state matches the expected chain
                    var chainTip = cursor.GetChainTip();
                    if (chainTip != chain.LastBlock)
                        throw new InvalidOperationException();
                }
            }
            catch (Exception)
            {
                // ensure any opened cursors are cleaned up on an error
                this.cursorCache.Dispose();
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

            this.cursorCache.Dispose();
        }

        public Chain Chain
        {
            get { return this.chain; }
        }

        public int UnspentTxCount
        {
            get
            {
                using (var handle = this.cursorCache.TakeItem())
                {
                    var cursor = handle.Item.Item;
                    return cursor.UnspentTxCount;
                }
            }
        }

        public bool ContainsUnspentTx(UInt256 txHash)
        {
            using (var handle = this.cursorCache.TakeItem())
            {
                var cursor = handle.Item.Item;
                return cursor.ContainsUnspentTx(txHash);
            }
        }

        public bool TryGetUnspentTx(UInt256 txHash, out UnspentTx unspentTx)
        {
            using (var handle = this.cursorCache.TakeItem())
            {
                var cursor = handle.Item.Item;
                return cursor.TryGetUnspentTx(txHash, out unspentTx);
            }
        }

        public IEnumerable<UnspentTx> ReadUnspentTransactions()
        {
            using (var handle = this.cursorCache.TakeItem())
            {
                var cursor = handle.Item.Item;
                foreach (var unspentTx in cursor.ReadUnspentTransactions())
                    yield return unspentTx;
            }
        }

        public bool ContainsBlockSpentTxes(int blockIndex)
        {
            using (var handle = this.cursorCache.TakeItem())
            {
                var cursor = handle.Item.Item;
                return cursor.ContainsBlockSpentTxes(blockIndex);
            }
        }

        public bool TryGetBlockSpentTxes(int blockIndex, out IImmutableList<SpentTx> spentTxes)
        {
            using (var handle = this.cursorCache.TakeItem())
            {
                var cursor = handle.Item.Item;
                return cursor.TryGetBlockSpentTxes(blockIndex, out spentTxes);
            }
        }
    }
}
