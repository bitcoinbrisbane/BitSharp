using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Storage;
using BitSharp.Network;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Data;
using System.Data.SqlClient;

namespace BitSharp.Storage.Esent
{
    public class BlockHeaderStorage : EsentDataStorage<BlockHeader>, IBlockHeaderStorage
    {
        public BlockHeaderStorage(string baseDirectory)
            : base(baseDirectory, "blockHeaders",
                blockHeader => StorageEncoder.EncodeBlockHeader(blockHeader),
                (blockHash, bytes) => StorageEncoder.DecodeBlockHeader(bytes, blockHash))
        { }
    }
}
