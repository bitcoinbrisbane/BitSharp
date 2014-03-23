using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class ValidationException : Exception
    {
        private readonly UInt256 blockHash;

        //[Obsolete]
        public ValidationException(UInt256 blockHash)
            : base()
        {
            this.blockHash = blockHash;
        }

        public ValidationException(UInt256 blockHash, string message)
            : base(message)
        {
            this.blockHash = blockHash;
        }

        public UInt256 BlockHash { get { return this.blockHash; } }
    }
}
