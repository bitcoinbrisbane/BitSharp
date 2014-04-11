using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public class BlockCompositeCache : IBoundedCache<UInt256, Block>
    {
        private readonly BlockHeaderCache blockHeaderCache;
        private readonly BlockTxHashesCache blockTxHashesCache;
        private readonly TransactionCache transactionCache;

        private readonly ConcurrentSetBuilder<UInt256> missingData;

        public BlockCompositeCache(BlockHeaderCache blockHeaderCache, BlockTxHashesCache blockTxHashesCache, TransactionCache transactionCache)
        {
            this.blockHeaderCache = blockHeaderCache;
            this.blockTxHashesCache = blockTxHashesCache;
            this.transactionCache = transactionCache;

            this.missingData = new ConcurrentSetBuilder<UInt256>();
        }

        public void Dispose()
        {
        }

        public event Action<UInt256, Block> OnAddition;

        public event Action<UInt256, Block> OnModification;

        public event Action<UInt256> OnRemoved;

        public event Action<UInt256> OnMissing;

        public string Name
        {
            get { return "Block View"; }
        }

        public ImmutableHashSet<UInt256> MissingData { get { return this.missingData.ToImmutable(); } }

        public int Count
        {
            get { return this.blockTxHashesCache.Count; }
        }

        public IEnumerable<UInt256> Keys
        {
            get { return this.blockTxHashesCache.Keys; }
        }

        public IEnumerable<Block> Values
        {
            get
            {
                foreach (var blockHash in this.blockTxHashesCache.Keys)
                {
                    Block block;
                    if (this.TryGetValue(blockHash, out block))
                        yield return block;
                }
            }
        }

        public bool ContainsKey(UInt256 blockHash)
        {
            return !this.missingData.Contains(blockHash)
                && this.blockHeaderCache.ContainsKey(blockHash)
                && this.blockTxHashesCache.ContainsKey(blockHash);
        }

        public bool TryGetValue(UInt256 blockHash, out Block block)
        {
            BlockHeader blockHeader;
            if (this.blockHeaderCache.TryGetValue(blockHash, out blockHeader))
            {
                IImmutableList<UInt256> blockTxHashes;
                if (this.blockTxHashesCache.TryGetValue(blockHeader.Hash, out blockTxHashes))
                {
                    if (blockHeader.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTxHashes))
                    {
                        var blockTransactions = new Transaction[blockTxHashes.Count];

                        var success = true;
                        var i = 0;
                        foreach (var txHash in blockTxHashes)
                        {
                            Transaction transaction;
                            if (this.transactionCache.TryGetValue(txHash, out transaction))
                            {
                                blockTransactions[i] = transaction;
                                i++;
                            }
                            else
                            {
                                success = false;
                                break;
                            }
                        }

                        if (success)
                        {
                            block = new Block(blockHeader, blockTransactions.ToImmutableArray());
                            this.missingData.Remove(blockHash);
                            return true;
                        }
                    }
                    else
                    {
                        Debugger.Break();
                    }
                }
            }

            block = default(Block);
            return false;
        }

        public bool TryAdd(UInt256 blockHash, Block block)
        {
            // return true if any TryAdd returns true during the writing of the block
            var result = false;

            // write the block header
            result |= this.blockHeaderCache.TryAdd(blockHash, block.Header);

            // write the block's transactions
            var txHashesList = ImmutableList.CreateBuilder<UInt256>();
            foreach (var tx in block.Transactions)
            {
                result |= this.transactionCache.TryAdd(tx.Hash, tx);
                txHashesList.Add(tx.Hash);
            }

            // write the transaction hash list
            result |= this.blockTxHashesCache.TryAdd(blockHash, txHashesList.ToImmutable());

            this.missingData.Remove(blockHash);

            //TODO should use knownKeys
            if (result)
            {
                var handler = this.OnAddition;
                if (handler != null)
                    handler(blockHash, block);
            }

            return result;
        }

        public bool TryRemove(UInt256 blockHash)
        {
            var result = false;

            // remove block header
            result |= this.blockHeaderCache.TryRemove(blockHash);

            // remove transactions
            IImmutableList<UInt256> txHashes;
            if (this.blockTxHashesCache.TryGetValue(blockHash, out txHashes))
            {
                foreach (var txHash in txHashes)
                    result |= this.transactionCache.TryRemove(txHash);
            }

            // remove transactions list
            result |= this.blockTxHashesCache.TryRemove(blockHash);

            //TODO should use knownKeys
            if (result)
            {
                var handler = this.OnRemoved;
                if (handler != null)
                    handler(blockHash);
            }

            return result;
        }

        public Block this[UInt256 blockHash]
        {
            get
            {
                Block block;
                if (this.TryGetValue(blockHash, out block))
                {
                    return block;
                }
                else
                {
                    this.missingData.Add(blockHash);

                    var handler = this.OnMissing;
                    if (handler != null)
                        handler(blockHash);

                    throw new MissingDataException(blockHash);
                }
            }
            set
            {
                // write the block header
                this.blockHeaderCache[value.Hash] = value.Header;

                // write the block's transactions
                var txHashesList = ImmutableList.CreateBuilder<UInt256>();
                foreach (var tx in value.Transactions)
                {
                    this.transactionCache[tx.Hash] = tx;
                    txHashesList.Add(tx.Hash);
                }

                // write the transaction hash list
                this.blockTxHashesCache[value.Hash] = txHashesList.ToImmutableList();

                this.missingData.Remove(blockHash);

                //TODO should use knownKeys
                var handler1 = this.OnAddition;
                if (handler1 != null)
                    handler1(blockHash, value);

                //TODO should use knownKeys
                var handler2 = this.OnModification;
                if (handler2 != null)
                    handler1(blockHash, value);
            }
        }

        public void Flush()
        {
            this.blockHeaderCache.Flush();
            this.blockTxHashesCache.Flush();
            this.transactionCache.Flush();
        }
    }
}
