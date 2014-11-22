using BitSharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.SqlServer.Azure
{
    public class BlockStorage : IBlockStorage
    {
        public bool ContainsChainedHeader(Common.UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public bool TryAddChainedHeader(Core.Domain.ChainedHeader chainedHeader)
        {
            throw new NotImplementedException();
        }

        public bool TryGetChainedHeader(Common.UInt256 blockHash, out Core.Domain.ChainedHeader chainedHeader)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveChainedHeader(Common.UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public Core.Domain.ChainedHeader FindMaxTotalWork()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Core.Domain.ChainedHeader> ReadChainedHeaders()
        {
            throw new NotImplementedException();
        }

        public bool IsBlockInvalid(Common.UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public void MarkBlockInvalid(Common.UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public void Flush()
        {
            throw new NotImplementedException();
        }

        public void Defragment()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
