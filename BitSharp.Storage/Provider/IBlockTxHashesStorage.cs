using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockTxHashesStorage : IBoundedStorage<UInt256, IImmutableList<UInt256>>
    {
    }
}
