using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Core;

namespace BitSharp.Esent
{
    public class ChainedHeaderStorage : EsentDataStorage<ChainedHeader>, IChainedHeaderStorage
    {
        public ChainedHeaderStorage(string baseDirectory)
            : base(baseDirectory, "ChainedHeaders",
                chainedHeader => DataEncoder.EncodeChainedHeader(chainedHeader),
                (blockHash, bytes) => DataEncoder.DecodeChainedHeader(bytes))
        { }
    }
}
