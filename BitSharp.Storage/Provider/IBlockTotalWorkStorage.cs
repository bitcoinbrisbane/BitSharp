using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public interface IBlockTotalWorkStorage : IBoundedStorage<UInt256, BigInteger>
    {
        IEnumerable<UInt256> SelectMaxTotalWorkBlocks();
    }
}
