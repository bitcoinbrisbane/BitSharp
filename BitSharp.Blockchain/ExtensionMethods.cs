using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Blockchain;
using BitSharp.Data;
using BitSharp.Storage;

namespace BitSharp.Blockchain.ExtensionMethods
{
    public static class BlockchainExtensionMethods
    {
        public static Block GetBlock(this CacheContext cacheContext, UInt256 blockHash)
        {
            Block block;
            if (!cacheContext.BlockCache.TryGetValue(blockHash, out block))
            {
                throw new MissingDataException(DataType.Block, blockHash);
            }

            return block;
        }

        public static BlockHeader GetBlockHeader(this CacheContext cacheContext, UInt256 blockHash)
        {
            BlockHeader blockHeader;
            if (!cacheContext.BlockHeaderCache.TryGetValue(blockHash, out blockHeader))
            {
                throw new MissingDataException(DataType.BlockHeader, blockHash);
            }

            return blockHeader;
        }

        public static ChainedBlock GetChainedBlock(this CacheContext cacheContext, UInt256 blockHash)
        {
            ChainedBlock chainedBlock;
            if (!cacheContext.ChainedBlockCache.TryGetValue(blockHash, out chainedBlock))
            {
                throw new MissingDataException(DataType.ChainedBlock, blockHash);
            }

            return chainedBlock;
        }

        public static Transaction GetTransaction(this CacheContext cacheContext, UInt256 txHash)
        {
            Transaction transaction;
            if (!cacheContext.TransactionCache.TryGetValue(txHash, out transaction))
            {
                throw new MissingDataException(DataType.Transaction, txHash);
            }

            return transaction;
        }
    }
}
