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
            return this.Data.Keys.Select(x => new UInt256(x));
        }

        public IEnumerable<KeyValuePair<UInt256, ChainedBlock>> ReadAllValues()
        {
            return this.Data.Select(x =>
                new KeyValuePair<UInt256, ChainedBlock>(new UInt256(x.Key), StorageEncoder.DecodeChainedBlock(x.Value.ToMemoryStream())));
        }

        public bool TryReadValue(UInt256 blockHash, out ChainedBlock chainedBlock)
        {
            byte[] chainedBlockBytes;
            if (this.Data.TryGetValue(blockHash.ToByteArray(), out chainedBlockBytes))
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
            foreach (var keyPair in keyPairs)
                this.Data[keyPair.Key.ToByteArray()] = StorageEncoder.EncodeChainedBlock(keyPair.Value.Value);

            return true;
        }

        public void Truncate()
        {
            this.Data.Clear();
        }
    }
}
