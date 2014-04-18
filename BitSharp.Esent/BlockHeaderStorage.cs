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
using BitSharp.Core.Storage;
using BitSharp.Core.Domain;
using BitSharp.Core;

namespace BitSharp.Esent
{
    public class BlockHeaderStorage : EsentDataStorage<BlockHeader>, IBlockHeaderStorage
    {
        public BlockHeaderStorage(string baseDirectory)
            : base(baseDirectory, "BlockHeaders",
                blockHeader => DataEncoder.EncodeBlockHeader(blockHeader),
                (blockHash, bytes) => DataEncoder.DecodeBlockHeader(bytes, blockHash))
        { }
    }
}
