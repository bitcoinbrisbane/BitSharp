using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public enum DataType
    {
        Block,
        BlockHeader,
        ChainedBlock,
        Transaction
    }

    public class MissingDataException : Exception
    {
        private readonly object key;

        public MissingDataException(object key)
        {
        }

        public object Key { get { return this.key; } }
    }
}
