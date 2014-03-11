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
    public class BlockHeaderStorage : EsentDataStorage, IBlockHeaderStorage
    {
        public BlockHeaderStorage(EsentStorageContext storageContext)
            : base(storageContext, "blockHeaders")
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.Data.Keys;
        }

        public IEnumerable<KeyValuePair<UInt256, BlockHeader>> ReadAllValues()
        {
            return this.Data.Select(x =>
                new KeyValuePair<UInt256, BlockHeader>(x.Key, StorageEncoder.DecodeBlockHeader(x.Value.ToMemoryStream())));
        }

        public bool TryReadValue(UInt256 blockHash, out BlockHeader blockHeader)
        {
            byte[] blockHeaderBytes;
            if (this.Data.TryGetValue(blockHash, out blockHeaderBytes))
            {
                blockHeader = StorageEncoder.DecodeBlockHeader(blockHeaderBytes.ToMemoryStream(), blockHash);
                return true;
            }
            else
            {
                blockHeader = default(BlockHeader);
                return false;
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<BlockHeader>>> keyPairs)
        {
            foreach (var keyPair in keyPairs)
                this.Data[keyPair.Key] = StorageEncoder.EncodeBlockHeader(keyPair.Value.Value);

            this.Data.Flush();
            return true;
        }

        public void Truncate()
        {
            this.Data.Clear();
        }
    }
}
