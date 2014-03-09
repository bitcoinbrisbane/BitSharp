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
            return this.StorageContext.BlockTransactionsStorage.ReadAllKeys();
        }

        public IEnumerable<KeyValuePair<UInt256, Block>> ReadAllValues()
        {
            foreach (var blockHeader in this.CacheContext.BlockHeaderCache.StreamAllValues())
            {
                ImmutableList<UInt256> blockTxHashes;
                if (this.StorageContext.BlockTransactionsStorage.TryReadValue(blockHeader.Value.Hash, out blockTxHashes))
                {
                    if (blockHeader.Value.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTxHashes))
                    {
                        var blockTransactions = new Transaction[blockTxHashes.Count];

                        var success = true;
                        var txIndex = 0;
                        foreach (var txHash in blockTxHashes)
                        {
                            Transaction transaction;
                            if (this.StorageContext.TransactionStorage.TryReadValue(txHash, out transaction))
                            {
                                blockTransactions[txIndex] = transaction;
                            }
                            else
                            {
                                success = false;
                                break;
                            }
                            txIndex++;
                        }

                        if (success)
                            yield return new KeyValuePair<UInt256, Block>(blockHeader.Value.Hash, new Block(blockHeader.Value, blockTransactions.ToImmutableList()));
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
                ImmutableList<UInt256> blockTxHashes;
                if (this.StorageContext.BlockTransactionsStorage.TryReadValue(blockHeader.Hash, out blockTxHashes))
                {
                    if (blockHeader.MerkleRoot == DataCalculator.CalculateMerkleRoot(blockTxHashes))
                    {
                        var blockTransactions = new Transaction[blockTxHashes.Count];

                        var success = true;
                        var txIndex = 0;
                        foreach (var txHash in blockTxHashes)
                        {
                            Transaction transaction;
                            if (this.StorageContext.TransactionStorage.TryReadValue(txHash, out transaction))
                            {
                                blockTransactions[txIndex] = transaction;
                            }
                            else
                            {
                                success = false;
                                break;
                            }
                            txIndex++;
                        }

                        if (success)
                        {
                            value = new Block(blockHeader, blockTransactions.ToImmutableList());
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
            var writeBlockTransactions = new List<KeyValuePair<UInt256, WriteValue<ImmutableList<Transaction>>>>();
            var writeTransactions = new List<KeyValuePair<UInt256, WriteValue<Transaction>>>();

            foreach (var value in keyPairs)
            {
                var block = value.Value.Value;

                //TODO check return value
                this.StorageContext.BlockTransactionsStorage.TryWriteValue(block.Hash,
                    new WriteValue<ImmutableList<UInt256>>(block.Transactions.Select(x => x.Hash).ToImmutableList(), value.Value.IsCreate));

                //TODO check return value
                this.StorageContext.TransactionStorage.TryWriteValues(
                    block.Transactions.Select(x => new KeyValuePair<UInt256, WriteValue<Transaction>>(x.Hash, new WriteValue<Transaction>(x, value.Value.IsCreate))));
            }

            //return this.StorageContext.BlockTransactionsStorage.TryWriteValues(writeBlockTransactions);
            //TODO
            return true;
        }
    }
}
