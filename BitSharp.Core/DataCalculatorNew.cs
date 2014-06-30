using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core
{
    //TODO organize and name properly
    public static class DataCalculatorNew
    {
        public static IEnumerable<BlockElement> ReadBlockElements(UInt256 merkleRoot, IEnumerable<BlockElement> blockElements)
        {
            foreach (var blockElement in blockElements)
            {
                yield return blockElement;
            }
        }
    }
}
