using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class BlockStorage : IBoundedStorage<UInt256, Block>
    {
        private readonly CacheContext cacheContext;

        public BlockStorage(CacheContext cacheContext)
        {
            this.cacheContext = cacheContext;
        }

        public void Dispose()
        {
        }

        public int Count
        {
            get { return this.cacheContext.BlockTxHashesCache.Count; }
        }

        public ICollection<UInt256> Keys
        {
            get { return this.cacheContext.BlockTxHashesCache.Keys; }
        }

        public ICollection<Block> Values
        {
            get { return new SimpleCollection<Block>(() => this.Count, () => this.Select(x => x.Value).GetEnumerator()); }
        }

        public bool ContainsKey(UInt256 blockHash)
        {
            return this.cacheContext.BlockHeaderCache.ContainsKey(blockHash)
                && this.cacheContext.BlockTxHashesCache.ContainsKey(blockHash);
        }

        public bool TryGetValue(UInt256 blockHash, out Block block)
        {
            BlockHeader blockHeader;
            if (this.cacheContext.BlockHeaderCache.TryGetValue(blockHash, out blockHeader))
            {
                IImmutableList<UInt256> blockTxHashes;
                if (this.cacheContext.BlockTxHashesCache.TryGetValue(blockHeader.Hash, out blockTxHashes))
                {
                    if (blockHeader.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTxHashes))
                    {
                        var blockTransactions = ImmutableList.CreateBuilder<Transaction>();

                        var success = true;
                        foreach (var txHash in blockTxHashes)
                        {
                            Transaction transaction;
                            if (this.cacheContext.TransactionCache.TryGetValue(txHash, out transaction))
                            {
                                blockTransactions.Add(transaction);
                            }
                            else
                            {
                                success = false;
                                break;
                            }
                        }

                        if (success)
                        {
                            block = new Block(blockHeader, blockTransactions.ToImmutable());
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
            this[blockHash] = block;
            //TODO
            return true;
        }

        public Block this[UInt256 blockHash]
        {
            get
            {
                Block block;
                if (this.TryGetValue(blockHash, out block))
                    return block;
                else
                    throw new KeyNotFoundException();
            }
            set
            {
                // write the block header
                this.cacheContext.BlockHeaderCache[value.Hash] = value.Header;

                // write the block's transactions
                foreach (var tx in value.Transactions)
                    this.cacheContext.TransactionCache[tx.Hash] = tx;

                // write the transaction hash list
                this.cacheContext.BlockTxHashesCache[value.Hash] = value.Transactions.Select(x => x.Hash).ToImmutableList();
            }
        }

        public IEnumerator<KeyValuePair<UInt256, Block>> GetEnumerator()
        {
            foreach (var blockHeader in this.cacheContext.BlockHeaderCache.Values)
            {
                Block block;
                if (this.TryGetValue(blockHeader.Hash, out block))
                    yield return new KeyValuePair<UInt256, Block>(block.Hash, block);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
