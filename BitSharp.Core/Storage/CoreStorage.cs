using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public class CoreStorage : IDisposable
    {
        private readonly Logger logger;
        private readonly IStorageManager storageManager;
        private readonly IBlockStorage blockStorage;
        private readonly IBlockTxesStorage blockTxesStorage;

        //TODO contains needs synchronization
        private readonly ConcurrentDictionary<UInt256, ChainedHeader> cachedHeaders;

        private readonly ConcurrentSetBuilder<UInt256> missingHeaders;
        private readonly ConcurrentSetBuilder<UInt256> missingBlockTxes;

        public CoreStorage(IStorageManager storageManager, Logger logger)
        {
            this.logger = logger;
            this.storageManager = storageManager;
            this.blockStorage = storageManager.BlockStorage;
            this.blockTxesStorage = storageManager.BlockTxesStorage;

            this.cachedHeaders = new ConcurrentDictionary<UInt256, ChainedHeader>();

            this.missingHeaders = new ConcurrentSetBuilder<UInt256>();
            this.missingBlockTxes = new ConcurrentSetBuilder<UInt256>();

            foreach (var chainedHeader in this.blockStorage.ReadChainedHeaders())
            {
                this.cachedHeaders[chainedHeader.Hash] = chainedHeader;
            }
        }

        public void Dispose()
        {
        }

        public event Action<ChainedHeader> ChainedHeaderAdded;

        public event Action<ChainedHeader> BlockTxesAdded;

        public event Action<ChainedHeader> BlockTxesRemoved;

        public event Action<UInt256> BlockTxesMissed;

        public event Action<UInt256> BlockInvalidated;

        public IImmutableSet<UInt256> MissingHeaders { get { return this.missingHeaders.ToImmutable(); } }

        public IImmutableSet<UInt256> MissingBlockTxes { get { return this.missingBlockTxes.ToImmutable(); } }

        public int ChainedHeaderCount { get { return -1; } }

        public int BlockWithTxesCount { get { return this.blockTxesStorage.BlockCount; } }

        public bool ContainsChainedHeader(UInt256 blockHash)
        {
            ChainedHeader chainedHeader;
            return TryGetChainedHeader(blockHash, out chainedHeader);
        }

        public void AddGenesisBlock(ChainedHeader genesisHeader)
        {
            if (genesisHeader.Height != 0)
                throw new ArgumentException("genesisHeader");

            if (this.blockStorage.TryAddChainedHeader(genesisHeader))
            {
                this.cachedHeaders[genesisHeader.Hash] = genesisHeader;
                RaiseChainedHeaderAdded(genesisHeader);
            }
        }

        public bool TryGetChainedHeader(UInt256 blockHash, out ChainedHeader chainedHeader)
        {
            if (this.cachedHeaders.TryGetValue(blockHash, out chainedHeader))
            {
                return chainedHeader != null;
            }
            else if (this.blockStorage.TryGetChainedHeader(blockHash, out chainedHeader))
            {
                this.cachedHeaders[blockHash] = chainedHeader;
                this.missingHeaders.Remove(blockHash);
                return true;
            }
            else
            {
                this.cachedHeaders[blockHash] = null;
                return false;
            }
        }

        public ChainedHeader GetChainedHeader(UInt256 blockHash)
        {
            ChainedHeader chainedHeader;
            if (TryGetChainedHeader(blockHash, out chainedHeader))
            {
                return chainedHeader;
            }
            else
            {
                this.missingHeaders.Add(blockHash);
                throw new MissingDataException(blockHash);
            }
        }

        public bool TryChainHeader(BlockHeader blockHeader, out ChainedHeader chainedHeader)
        {
            if (TryGetChainedHeader(blockHeader.Hash, out chainedHeader))
            {
                return true;
            }
            else
            {
                ChainedHeader previousChainedHeader;
                if (this.blockStorage.TryGetChainedHeader(blockHeader.PreviousBlock, out previousChainedHeader))
                {
                    var headerWork = blockHeader.CalculateWork();
                    if (headerWork < 0)
                        return false;

                    chainedHeader = new ChainedHeader(blockHeader,
                        previousChainedHeader.Height + 1,
                        previousChainedHeader.TotalWork + headerWork);

                    if (this.blockStorage.TryAddChainedHeader(chainedHeader))
                    {
                        this.cachedHeaders[chainedHeader.Hash] = chainedHeader;
                        this.missingHeaders.Remove(chainedHeader.Hash);
                        RaiseChainedHeaderAdded(chainedHeader);
                        return true;
                    }
                    else
                    {
                        this.logger.Warn("Unexpected condition: validly chained header could not be added");
                    }
                }
            }

            chainedHeader = default(ChainedHeader);
            return false;
        }

        public ChainedHeader FindMaxTotalWork()
        {
            return this.blockStorage.FindMaxTotalWork();
        }

        public bool ContainsBlockTxes(UInt256 blockHash)
        {
            return this.blockTxesStorage.ContainsBlock(blockHash);
        }

        public bool TryAddBlock(Block block)
        {
            ChainedHeader chainedHeader;
            if (TryChainHeader(block.Header, out chainedHeader))
            {
                if (this.blockTxesStorage.TryAddBlockTransactions(block.Hash, block.Transactions))
                {
                    this.missingBlockTxes.Remove(block.Hash);
                    RaiseBlockTxesAdded(chainedHeader);
                    return true;
                }
            }

            return false;
        }

        public bool TryGetBlock(UInt256 blockHash, out Block block)
        {
            ChainedHeader chainedHeader;
            if (!TryGetChainedHeader(blockHash, out chainedHeader))
            {
                block = default(Block);
                return false;
            }

            ImmutableArray<Transaction>.Builder transactions = ImmutableArray.CreateBuilder<Transaction>();
            try
            {
                transactions.AddRange(
                    ReadBlockTransactions(chainedHeader.Hash, chainedHeader.MerkleRoot, requireTransactions: true)
                        .Select(x => x.Transaction));
            }
            catch (Exception)
            {
                block = default(Block);
                return false;
            }

            block = new Block(chainedHeader.BlockHeader, transactions.ToImmutable());
            return true;
        }

        public bool TryGetTransaction(UInt256 blockHash, int txIndex, out Transaction transaction)
        {
            return this.blockTxesStorage.TryGetTransaction(blockHash, txIndex, out transaction);
        }

        public IEnumerable<BlockTx> ReadBlockTransactions(UInt256 blockHash, UInt256 merkleRoot, bool requireTransactions = false)
        {
            IEnumerator<BlockTx> blockTxes;
            try
            {
                blockTxes = MerkleTree.ReadMerkleTreeNodes(merkleRoot, this.blockTxesStorage.ReadBlockTransactions(blockHash)).GetEnumerator();
            }
            catch (Exception)
            {
                this.missingBlockTxes.Add(blockHash);
                RaiseBlockTxesMissed(blockHash);
                throw;
            }
            using (blockTxes)
            {
                while (true)
                {
                    bool result;
                    try
                    {
                        result = blockTxes.MoveNext();
                    }
                    catch (Exception)
                    {
                        this.missingBlockTxes.Add(blockHash);
                        RaiseBlockTxesMissed(blockHash);
                        throw;
                    }

                    if (!result)
                        break;

                    var blockTx = blockTxes.Current;

                    if (requireTransactions && blockTx.Pruned)
                    {
                        //TODO distinguish different kinds of missing: pruned and missing entirely

                        //this.containsBlockTxes[blockHash] = false;
                        //this.missingBlockTxes.Add(blockHash);
                        RaiseBlockTxesMissed(blockHash);
                        throw new MissingDataException(blockHash);
                    }

                    yield return blockTx;
                }
            }
        }

        public void PruneElements(UInt256 blockHash, IEnumerable<int> txIndices)
        {
            this.blockTxesStorage.PruneElements(blockHash, txIndices);
        }

        public bool IsBlockInvalid(UInt256 blockHash)
        {
            return this.blockStorage.IsBlockInvalid(blockHash);
        }

        internal IChainStateCursor OpenChainStateCursor()
        {
            return this.storageManager.OpenChainStateCursor();
        }

        internal void MarkBlockInvalid(UInt256 blockHash)
        {
            this.blockStorage.MarkBlockInvalid(blockHash);
            RaiseBlockInvalidated(blockHash);
        }

        internal void Defragment()
        {
            this.blockStorage.Defragment();
            this.blockTxesStorage.Defragment();
        }

        private void RaiseChainedHeaderAdded(ChainedHeader chainedHeader)
        {
            var handler = this.ChainedHeaderAdded;
            if (handler != null)
                handler(chainedHeader);
        }

        private void RaiseBlockTxesAdded(ChainedHeader chainedHeader)
        {
            var handler = this.BlockTxesAdded;
            if (handler != null)
                handler(chainedHeader);
        }

        private void RaiseBlockTxesMissed(UInt256 blockHash)
        {
            var handler = this.BlockTxesMissed;
            if (handler != null)
                handler(blockHash);
        }

        private void RaiseBlockInvalidated(UInt256 blockHash)
        {
            var handler = this.BlockInvalidated;
            if (handler != null)
                handler(blockHash);
        }
    }
}
