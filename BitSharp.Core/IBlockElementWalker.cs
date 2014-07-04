using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core
{
    public interface IBlockElementWalker
    {
        bool TryMoveToIndex(int index, out BlockElement element);

        void WriteElement(BlockElement element);

        IEnumerable<BlockElement> StreamElements();
    }
}
