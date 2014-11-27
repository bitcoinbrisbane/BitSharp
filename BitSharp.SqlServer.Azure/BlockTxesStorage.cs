using BitSharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Sql.Azure
{
    public class BlockTxesStorage : IBlockTxesStorage
    {
        public int BlockCount
        {
            get { throw new NotImplementedException(); }
        }

        public bool ContainsBlock(Common.UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public bool TryAddBlockTransactions(Common.UInt256 blockHash, IEnumerable<Core.Domain.Transaction> transactions)
        {
            throw new NotImplementedException();
        }

        public bool TryGetTransaction(Common.UInt256 blockHash, int txIndex, out Core.Domain.Transaction transaction)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveBlockTransactions(Common.UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public bool TryReadBlockTransactions(Common.UInt256 blockHash, out IEnumerable<Core.Domain.BlockTx> blockTxes)
        {
            throw new NotImplementedException();
        }

        public void PruneElements(Common.UInt256 blockHash, IEnumerable<int> txIndices)
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
