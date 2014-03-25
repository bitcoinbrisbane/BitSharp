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

namespace BitSharp.Storage.MongoDB
{
    public class ChainedBlockStorage : MongoDBDataStorage<ChainedBlock>
    {
        public ChainedBlockStorage(MongoDBStorageContext storageContext)
            : base(storageContext, "chainedBlocks",
                chainedBlock => StorageEncoder.EncodeChainedBlock(chainedBlock),
                (blockHash, bytes) => StorageEncoder.DecodeChainedBlock(bytes))
        { }
    }
}
