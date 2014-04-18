using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Test
{
    public class FakeHeaders
    {
        private static int staticNonce = 0;

        private readonly List<BlockHeader> blockHeaders;
        private readonly UInt32 nonce;

        public FakeHeaders()
        {
            this.blockHeaders = new List<BlockHeader>();
            this.nonce = (UInt32)Interlocked.Increment(ref staticNonce);
        }

        public FakeHeaders(FakeHeaders parent)
        {
            this.blockHeaders = new List<BlockHeader>(parent.blockHeaders);
            this.nonce = (UInt32)Interlocked.Increment(ref staticNonce);
        }

        public BlockHeader Genesis()
        {
            if (this.blockHeaders.Count > 0)
                throw new InvalidOperationException();

            var blockHeader = new BlockHeader(0, 0, 0, 0, 0, this.nonce);

            this.blockHeaders.Add(blockHeader);
            return blockHeader;
        }

        public BlockHeader Next()
        {
            if (this.blockHeaders.Count == 0)
                throw new InvalidOperationException();

            var prevBlockHeader = this.blockHeaders.Last();
            var blockHeader = new BlockHeader(0, prevBlockHeader.Hash, 0, 0, 0, this.nonce);

            this.blockHeaders.Add(blockHeader);
            return blockHeader;
        }

        public BlockHeader this[int i]
        {
            get { return this.blockHeaders[i]; }
        }
    }
}
