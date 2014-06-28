using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public class CoreStorageInstance : ICoreStorage
    {
        public CoreStorageInstance()
        {
        }

        public BlockHeaderCache BlockHeaderCache
        {
            get { throw new NotImplementedException(); }
        }

        public ChainedHeaderCache ChainedHeaderCache
        {
            get { throw new NotImplementedException(); }
        }

        public IBlockStorageNew BlockCache
        {
            get { throw new NotImplementedException(); }
        }

        public BlockTxHashesCache BlockTxHashesCache
        {
            get { throw new NotImplementedException(); }
        }

        public TransactionCache TransactionCache
        {
            get { throw new NotImplementedException(); }
        }

        public SpentTransactionsCache SpentTransactionsCache
        {
            get { throw new NotImplementedException(); }
        }

        public SpentOutputsCache SpentOutputsCache
        {
            get { throw new NotImplementedException(); }
        }

        public InvalidBlockCache InvalidBlockCache
        {
            get { throw new NotImplementedException(); }
        }
    }
}
