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
        IBlockHeaderStorage BlockHeaderStorage { get; }

        IChainedHeaderStorage ChainedHeaderStorage { get; }

        IInvalidBlockStorage InvalidBlockStorage { get; }

        IBlockStorageNew BlockStorage { get; }

        IChainStateBuilderStorage CreateOrLoadChainState(ChainedHeader genesisHeader);
    }
}
