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
    public class ChainedBlockStorage : EsentDataStorage, IChainedBlockStorage
    {
        public ChainedBlockStorage(EsentStorageContext storageContext)
            : base(storageContext, "chainedBlocks")
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.ReadAllDataKeys().Select(x => new UInt256(x));
        }

        public IEnumerable<KeyValuePair<UInt256, ChainedBlock>> ReadAllValues()
        {
            return this.ReadAllDataValues().Select(x =>
                new KeyValuePair<UInt256, ChainedBlock>(new UInt256(x.Key), StorageEncoder.DecodeChainedBlock(x.Value.ToMemoryStream())));
        }

        public bool TryReadValue(UInt256 blockHash, out ChainedBlock chainedBlock)
        {
            byte[] chainedBlockBytes;
            if (this.TryReadDataValue(blockHash.ToByteArray(), out chainedBlockBytes))
            {
                chainedBlock = StorageEncoder.DecodeChainedBlock(chainedBlockBytes.ToMemoryStream());
                return true;
            }
            else
            {
                chainedBlock = default(ChainedBlock);
                return false;
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<ChainedBlock>>> keyPairs)
        {
            return this.TryWriteDataValues(keyPairs.Select(x => new KeyValuePair<byte[], WriteValue<byte[]>>(x.Key.ToByteArray(), new WriteValue<byte[]>(StorageEncoder.EncodeChainedBlock(x.Value.Value), x.Value.IsCreate))));
        }

        public void Truncate()
        {
            this.TruncateData();
        }
    }
}
