using BitSharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.SqlServer.Azure
{
    public class SqlStorageManager : IStorageManager
    {
        public IBlockStorage BlockStorage
        {
            get;
            private set;
        }

        public IBlockTxesStorage BlockTxesStorage
        {
            get;
            private set;
        }

        public Common.DisposeHandle<IChainStateCursor> OpenChainStateCursor()
        {
            throw new NotImplementedException();
        }

        public SqlStorageManager()
        {
            this.BlockStorage = new SqlServer.Azure.BlockStorage();
            this.BlockTxesStorage = new Sql.Azure.BlockTxesStorage();
        }

        public void Dispose()
        {
        }
    }
}
