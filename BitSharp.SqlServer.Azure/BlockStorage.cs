using BitSharp.Core.Storage;
using BitSharp.Sql.Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.SqlServer.Azure
{
    public class BlockStorage : IBlockStorage
    {
        private BitcoinEntities db = new BitcoinEntities();

        public bool ContainsChainedHeader(Common.UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public bool TryAddChainedHeader(Core.Domain.ChainedHeader chainedHeader)
        {
            db.ChainedHeaders.Add(new ChainedHeader()
            {
                Bits = chainedHeader.Bits,
                Hash = chainedHeader.Hash.ToByteArray(),
                Nonce = chainedHeader.Nonce, //,
                Version = Convert.ToInt16(chainedHeader.Version)
                //TimeStamp = chainedHeader.Time
            });

            db.SaveChanges();
            return true;
        }

        public bool TryGetChainedHeader(Common.UInt256 blockHash, out Core.Domain.ChainedHeader chainedHeader)
        {
            ChainedHeader header = db.ChainedHeaders.FirstOrDefault(x => x.Hash == blockHash.ToByteArray());

            chainedHeader = null; // new Core.Domain.ChainedHeader(new Core.Domain.BlockHeader(), 1, 1);

            if (header != null)
            {
                return true;
            }
            else
            {
                return false;
            }
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
        }

        public void Defragment()
        {
        }

        public void Dispose()
        {
        }
    }
}
