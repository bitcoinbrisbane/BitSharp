using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IStorageManager : IDisposable
    {
        IBlockStorage BlockStorage { get; }

        IBlockTxesStorage BlockTxesStorage { get; }

        IChainStateCursor CreateOrLoadChainState(ChainedHeader genesisHeader);
    }
}
