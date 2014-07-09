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
    public class MemoryBlockStorageNew : IBlockStorageNew
    {
        private readonly ConcurrentDictionary<UInt256, Block> blocks;

        public MemoryBlockStorageNew()
        {
            this.blocks = new ConcurrentDictionary<UInt256, Block>();
        }

        public void Dispose()
        {
        }

        public event Action<UInt256, Block> OnAddition;

        public event Action<UInt256, Block> OnModification;

        public event Action<UInt256> OnRemoved;

        public event Action<UInt256> OnMissing;

        public void AddBlock(Block block)
        {
            this.blocks.TryAdd(block.Hash, block);
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

        public IEnumerable<BlockTx> ReadBlock(UInt256 blockHash, UInt256 merkleRoot)
        {
            Block block;
            if (this.blocks.TryGetValue(blockHash, out block))
            {
                return DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot,
                    block.Transactions.Select((tx, txIndex) => new BlockTx(txIndex, 0, tx.Hash, tx)));
            }
            else
            {
                throw new MissingDataException(blockHash);
            }
        }

        public IEnumerable<BlockElement> ReadBlockElements(UInt256 blockHash, UInt256 merkleRoot)
        {
            Block block;
            if (this.blocks.TryGetValue(blockHash, out block))
            {
                return DataCalculatorNew.ReadMerkleTreeNodes(merkleRoot,
                    block.Transactions.Select((tx, txIndex) => new BlockElement(txIndex, 0, tx.Hash, false)));
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

        public ImmutableHashSet<UInt256> MissingData
        {
            get { throw new NotImplementedException(); }
        }

        public bool ContainsKey(UInt256 blockHash)
        {
            return this.blocks.ContainsKey(blockHash);
        }

        public bool TryGetValue(UInt256 blockHash, out Block block)
        {
            return this.blocks.TryGetValue(blockHash, out block);
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
