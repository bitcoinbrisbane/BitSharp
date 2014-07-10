using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage.Memory
{
    public class MemoryBlockStorage : IBlockStorage
    {
        private readonly ConcurrentDictionary<UInt256, ChainedHeader> chainedHeaders;

        public MemoryBlockStorage()
        {
            this.chainedHeaders = new ConcurrentDictionary<UInt256, ChainedHeader>();
        }

        public void Dispose()
        {
        }

        public bool ContainsChainedHeader(UInt256 blockHash)
        {
            return this.chainedHeaders.ContainsKey(blockHash);
        }

        public bool TryAddChainedHeader(ChainedHeader chainedHeader)
        {
            return this.chainedHeaders.TryAdd(chainedHeader.Hash, chainedHeader);
        }

        public bool TryGetChainedHeader(UInt256 blockHash, out ChainedHeader chainedHeader)
        {
            return this.chainedHeaders.TryGetValue(blockHash, out chainedHeader);
        }

        public ChainedHeader FindMaxTotalWork()
        {
            ChainedHeader maxTotalWorkHeader = null;

            foreach (var chainedHeader in this.chainedHeaders.Values)
            {
                if (maxTotalWorkHeader == null || chainedHeader.TotalWork > maxTotalWorkHeader.TotalWork)
                {
                    maxTotalWorkHeader = chainedHeader;
                }
            }

            return maxTotalWorkHeader;
        }

        public IEnumerable<ChainedHeader> ReadChainedHeaders()
        {
            return this.chainedHeaders.Values;
        }
    }
}
