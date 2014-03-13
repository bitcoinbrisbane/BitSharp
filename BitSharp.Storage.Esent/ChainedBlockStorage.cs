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
using System.IO;
using Microsoft.Isam.Esent.Collections.Generic;

namespace BitSharp.Storage.Esent
{
    public class ChainedBlockStorage : IChainedBlockStorage
    {
        private readonly EsentStorageContext storageContext;
        private readonly string name;
        private readonly string dataPath;
        private readonly PersistentDictionary<string, ChainedBlockSerial> data;

        public ChainedBlockStorage(EsentStorageContext storageContext)
        {
            this.storageContext = storageContext;
            this.name = "chainedBlocks";
            this.dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "data", this.name);
            this.data = new PersistentDictionary<string, ChainedBlockSerial>(this.dataPath);
        }

        public EsentStorageContext StorageContext { get { return this.storageContext; } }

        public void Dispose()
        {
            this.data.Dispose();
        }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.data.Keys.Select(x => DecodeKey(x));
        }

        public IEnumerable<KeyValuePair<UInt256, ChainedBlock>> ReadAllValues()
        {
            return this.data.Select(keyPair =>
            {
                var blockHash = DecodeKey(keyPair.Key);
                var chainedBlock = DecodeValue(blockHash, keyPair.Value);
                return new KeyValuePair<UInt256, ChainedBlock>(blockHash, chainedBlock);
            });
        }

        public bool TryReadValue(UInt256 blockHash, out ChainedBlock chainedBlock)
        {
            ChainedBlockSerial chainedBlockSerial;
            if (this.data.TryGetValue(EncodeKey(blockHash), out chainedBlockSerial))
            {
                chainedBlock = DecodeValue(blockHash, chainedBlockSerial);
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
                this.data[EncodeKey(keyPair.Key)] = EncodeValue(keyPair.Value.Value);

            this.data.Flush();
            return true;
        }

        public void Truncate()
        {
            this.data.Clear();
        }

        public IEnumerable<KeyValuePair<UInt256, ChainedBlock>> SelectMaxTotalWorkBlocks()
        {
            var maxTotalWork = this.data.Values.Max(x => StorageEncoder.DecodeTotalWork(Convert.FromBase64String(x.TotalWork).ToMemoryStream()));
            var maxTotalWorkString = Convert.ToBase64String(StorageEncoder.EncodeTotalWork(maxTotalWork));

            return this.data
                .Where(x => x.Value.TotalWork == maxTotalWorkString)
                .Select(keyPair =>
                {
                    var blockHash = DecodeKey(keyPair.Key);
                    var chainedBlock = DecodeValue(blockHash, keyPair.Value);
                    return new KeyValuePair<UInt256, ChainedBlock>(blockHash, chainedBlock);
                });
        }

        private static ChainedBlockSerial EncodeValue(ChainedBlock chainedBlock)
        {
            return new ChainedBlockSerial
            {
                PreviousBlockHash = Convert.ToBase64String(chainedBlock.PreviousBlockHash.ToByteArray()),
                Height = chainedBlock.Height,
                TotalWork = Convert.ToBase64String(StorageEncoder.EncodeTotalWork(chainedBlock.TotalWork))
            };
        }

        private static string EncodeKey(UInt256 value)
        {
            return Convert.ToBase64String(value.ToByteArray());
        }

        private static ChainedBlock DecodeValue(UInt256 blockHash, ChainedBlockSerial value)
        {
            return new ChainedBlock(
                blockHash: blockHash,
                previousBlockHash: new UInt256(Convert.FromBase64String(value.PreviousBlockHash)),
                height: value.Height,
                totalWork: StorageEncoder.DecodeTotalWork(Convert.FromBase64String(value.TotalWork).ToMemoryStream())
            );
        }

        private static UInt256 DecodeKey(string key)
        {
            return new UInt256(Convert.FromBase64String(key));
        }

        [Serializable]
        private struct ChainedBlockSerial
        {
            public string PreviousBlockHash;
            public int Height;
            public string TotalWork;
        }
    }
}