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
        private readonly CacheContext _cacheContext;

        public BlockStorage(CacheContext cacheContext)
        {
            this._cacheContext = cacheContext;
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public void Dispose()
        {
        }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.CacheContext.BlockTxHashesCache.GetAllKeys();
        }

        public IEnumerable<KeyValuePair<UInt256, Block>> ReadAllValues()
        {
            foreach (var blockHeader in this.CacheContext.BlockHeaderCache.StreamAllValues())
            {
                IImmutableList<UInt256> blockTxHashes;
                if (this.CacheContext.BlockTxHashesCache.TryGetValue(blockHeader.Value.Hash, out blockTxHashes))
                {
                    if (blockHeader.Value.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTxHashes))
                    {
                        var blockTransactions = ImmutableList.CreateBuilder<Transaction>();

                        var success = true;
                        foreach (var txHash in blockTxHashes)
                        {
                            Transaction transaction;
                            if (this.CacheContext.TransactionCache.TryGetValue(txHash, out transaction))
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
                            yield return new KeyValuePair<UInt256, Block>(blockHeader.Value.Hash, new Block(blockHeader.Value, blockTransactions.ToImmutable()));
                    }
                    else
                    {
                        Debugger.Break();
                    }
                }
            }
        }

        public bool TryReadValue(UInt256 key, out Block value)
        {
            BlockHeader blockHeader;
            if (this.CacheContext.BlockHeaderCache.TryGetValue(key, out blockHeader))
            {
                IImmutableList<UInt256> blockTxHashes;
                if (this.CacheContext.BlockTxHashesCache.TryGetValue(blockHeader.Hash, out blockTxHashes))
                {
                    if (blockHeader.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTxHashes))
                    {
                        var blockTransactions = ImmutableList.CreateBuilder<Transaction>();

                        var success = true;
                        foreach (var txHash in blockTxHashes)
                        {
                            Transaction transaction;
                            if (this.CacheContext.TransactionCache.TryGetValue(txHash, out transaction))
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
                            value = new Block(blockHeader, blockTransactions.ToImmutable());
                            return true;
                        }
                    }
                    else
                    {
                        Debugger.Break();
                    }
                }
            }

            value = default(Block);
            return false;
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<Block>>> keyPairs)
        {
            foreach (var keyPair in keyPairs)
            {
                var block = keyPair.Value.Value;

                // write the block's transactions
                if (!this.StorageContext.TransactionStorage.TryWriteValues(
                    block.Transactions.Select(x => new KeyValuePair<UInt256, WriteValue<Transaction>>(x.Hash, new WriteValue<Transaction>(x, keyPair.Value.IsCreate)))))
                {
                    return false;
                }

                // then write the transaction hash list
                if (!this.StorageContext.BlockTxHashesStorage.TryWriteValue(block.Hash, block.Transactions.Select(x => x.Hash).ToImmutableList(), keyPair.Value.IsCreate))
                {
                    return false;
                }
            }

            //TODO
            //this.StorageContext.TransactionStorage.Flush();
            //this.StorageContext.BlockTxHashesStorage.Flush();

            return true;
        }
    }
}
