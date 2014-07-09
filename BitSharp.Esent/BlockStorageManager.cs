using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    public class BlockStorageManager : IBlockStorageNew
    {
        private readonly BlockStorageNew blockStorage1;
        private readonly BlockStorageNew blockStorage2;

        public BlockStorageManager()
        {
            this.blockStorage1 = new BlockStorageNew(@"D:\BitSharp.Blocks");
            this.blockStorage2 = new BlockStorageNew(@"X:\BitSharp.Blocks");

            this.blockStorage1.OnAddition += HandleOnAddition;
            this.blockStorage1.OnModification += HandleOnModification;
            this.blockStorage1.OnRemoved += HandleOnRemoved;
            this.blockStorage1.OnMissing += HandleOnMissing;

            this.blockStorage2.OnAddition += HandleOnAddition;
            this.blockStorage2.OnModification += HandleOnModification;
            this.blockStorage2.OnRemoved += HandleOnRemoved;
            this.blockStorage2.OnMissing += HandleOnMissing;
        }

        public void Dispose()
        {
            this.blockStorage1.OnAddition -= HandleOnAddition;
            this.blockStorage1.OnModification -= HandleOnModification;
            this.blockStorage1.OnRemoved -= HandleOnRemoved;
            this.blockStorage1.OnMissing -= HandleOnMissing;

            this.blockStorage2.OnAddition -= HandleOnAddition;
            this.blockStorage2.OnModification -= HandleOnModification;
            this.blockStorage2.OnRemoved -= HandleOnRemoved;
            this.blockStorage2.OnMissing -= HandleOnMissing;

            this.blockStorage1.Dispose();
            this.blockStorage2.Dispose();
        }

        public event Action<UInt256, Block> OnAddition;

        public event Action<UInt256, Block> OnModification;

        public event Action<UInt256> OnRemoved;

        public event Action<UInt256> OnMissing;


        public void AddBlock(Block block)
        {
            var blockStorage = GetBlockStorage(block.Hash);
            blockStorage.AddBlock(block);
        }

        public bool TryGetTransaction(UInt256 blockHash, int txIndex, out Transaction transaction)
        {
            var blockStorage = GetBlockStorage(blockHash);
            return blockStorage.TryGetTransaction(blockHash, txIndex, out transaction);
        }

        public IEnumerable<BlockTx> ReadBlock(UInt256 blockHash, UInt256 merkleRoot)
        {
            var blockStorage = GetBlockStorage(blockHash);
            return blockStorage.ReadBlock(blockHash, merkleRoot);
        }

        public IEnumerable<BlockElement> ReadBlockElements(UInt256 blockHash, UInt256 merkleRoot)
        {
            var blockStorage = GetBlockStorage(blockHash);
            return blockStorage.ReadBlockElements(blockHash, merkleRoot);
        }

        public void PruneElements(UInt256 blockHash, IEnumerable<int> indices)
        {
            var blockStorage = GetBlockStorage(blockHash);
            blockStorage.PruneElements(blockHash, indices);
        }

        public int Count
        {
            get
            {
                return this.blockStorage1.Count + this.blockStorage2.Count;
            }
        }

        public string Name
        {
            get { return "BlockStorageManager"; }
        }

        public ImmutableHashSet<UInt256> MissingData
        {
            get
            {
                return this.blockStorage1.MissingData.Union(this.blockStorage2.MissingData);
            }
        }

        public bool ContainsKey(UInt256 blockHash)
        {
            var blockStorage = GetBlockStorage(blockHash);
            return blockStorage.ContainsKey(blockHash);
        }

        public bool TryGetValue(UInt256 blockHash, out Block block)
        {
            var blockStorage = GetBlockStorage(blockHash);
            return blockStorage.TryGetValue(blockHash, out block);
        }

        public bool TryAdd(UInt256 blockHash, Block block)
        {
            var blockStorage = GetBlockStorage(blockHash);
            return blockStorage.TryAdd(blockHash, block);
        }

        public bool TryRemove(UInt256 blockHash)
        {
            var blockStorage = GetBlockStorage(blockHash);
            return blockStorage.TryRemove(blockHash);
        }

        public void Flush()
        {
            this.blockStorage1.Flush();
            this.blockStorage2.Flush();
        }

        private BlockStorageNew GetBlockStorage(UInt256 blockHash)
        {
            //TODO don't use hash, ensure blocks are evenly distributed
            if (blockHash % 2 == 0)
                return this.blockStorage1;
            else
                return this.blockStorage2;
        }

        private void HandleOnAddition(UInt256 blockHash, Block block)
        {
            var handler = this.OnAddition;
            if (handler != null)
                handler(blockHash, block);
        }

        private void HandleOnModification(UInt256 blockHash, Block block)
        {
            var handler = this.OnModification;
            if (handler != null)
                handler(blockHash, block);
        }

        private void HandleOnRemoved(UInt256 blockHash)
        {
            var handler = this.OnRemoved;
            if (handler != null)
                handler(blockHash);
        }

        private void HandleOnMissing(UInt256 blockHash)
        {
            var handler = this.OnMissing;
            if (handler != null)
                handler(blockHash);
        }
    }
}
