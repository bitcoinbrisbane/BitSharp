using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    //TODO lots unimplemented in here while interfaces get cleaned up
    public class MemoryBlockTxesStorage : IBlockTxesStorage
    {
        private readonly ConcurrentDictionary<UInt256, Block> blocks;

        public MemoryBlockTxesStorage()
        {
            this.blocks = new ConcurrentDictionary<UInt256, Block>();
        }

        public void Dispose()
        {
        }

        public bool TryGetTransaction(UInt256 blockHash, int txIndex, out Transaction transaction)
        {
            Block block;
            if (this.blocks.TryGetValue(blockHash, out block)
                && txIndex < block.Transactions.Length)
            {
                transaction = block.Transactions[txIndex];
                return true;
            }
            else
            {
                transaction = default(Transaction);
                return false;
            }
        }

        public IEnumerable<BlockTx> ReadBlockTransactions(UInt256 blockHash)
        {
            Block block;
            if (this.blocks.TryGetValue(blockHash, out block))
            {
                return block.Transactions.Select((tx, txIndex) => new BlockTx(txIndex, 0, tx.Hash, /*pruned:*/false, tx));
            }
            else
            {
                throw new MissingDataException(blockHash);
            }
        }

        public void PruneElements(UInt256 blockHash, IEnumerable<int> indices)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return this.blocks.Count; }
        }

        public string Name
        {
            get { return "MemoryBlockStorage"; }
        }

        public bool ContainsBlock(UInt256 blockHash)
        {
            return this.blocks.ContainsKey(blockHash);
        }

        public bool TryAdd(UInt256 blockHash, Block block)
        {
            return this.blocks.TryAdd(blockHash, block);
        }

        public bool TryRemove(UInt256 blockHash)
        {
            Block block;
            return this.blocks.TryRemove(blockHash, out block);
        }

        public void Flush()
        {
        }
    }
}
