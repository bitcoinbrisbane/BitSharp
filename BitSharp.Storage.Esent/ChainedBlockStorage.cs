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
using System.Numerics;

namespace BitSharp.Storage.Esent
{
    public class ChainedBlockStorage : IChainedBlockStorage
    {
        private readonly EsentStorageContext storageContext;
        private readonly string name;
        private readonly string directory;
        private readonly PersistentDictionary<string, ChainedBlockSerial> dict;

        public ChainedBlockStorage(EsentStorageContext storageContext)
        {
            this.storageContext = storageContext;
            this.name = "chainedBlocks";
            this.directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "data", this.name);
            this.dict = new PersistentDictionary<string, ChainedBlockSerial>(this.directory);
        }

        public EsentStorageContext StorageContext { get { return this.storageContext; } }

        public void Dispose()
        {
            this.dict.Dispose();
        }

        public int Count
        {
            get { return this.dict.Count; }
        }

        public IEnumerable<UInt256> Keys
        {
            get { return this.dict.Keys.Select(x => DecodeKey(x)); }
        }

        public IEnumerable<ChainedBlock> Values
        {
            get { return this.Select(x => x.Value); }
        }

        public bool ContainsKey(UInt256 blockHash)
        {
            return this.dict.ContainsKey(EncodeKey(blockHash));
        }

        public bool TryGetValue(UInt256 blockHash, out ChainedBlock chainedBlock)
        {
            ChainedBlockSerial chainedBlockSerial;
            if (this.dict.TryGetValue(EncodeKey(blockHash), out chainedBlockSerial))
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

        public bool TryAdd(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            if (!this.ContainsKey(blockHash))
            {
                try
                {
                    this.dict.Add(EncodeKey(blockHash), EncodeValue(chainedBlock));
                    return true;
                }
                catch (ArgumentException)
                { return false; }
            }
            else
            {
                return false;
            }
        }

        public ChainedBlock this[UInt256 blockHash]
        {
            get
            {
                ChainedBlock chainedBlock;
                if (this.TryGetValue(blockHash, out chainedBlock))
                    return chainedBlock;

                throw new KeyNotFoundException();
            }
            set
            {
                this.dict[EncodeKey(blockHash)] = EncodeValue(value);
            }
        }

        public IEnumerator<KeyValuePair<UInt256, ChainedBlock>> GetEnumerator()
        {
            foreach (var keyPair in this.dict)
            {
                var blockHash = DecodeKey(keyPair.Key);
                yield return new KeyValuePair<UInt256, ChainedBlock>(blockHash, DecodeValue(blockHash, keyPair.Value));
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerable<ChainedBlock> SelectMaxTotalWorkBlocks()
        {
            try
            {
                var maxTotalWork = new BigInteger(-1);
                var maxTotalWorkBlocks = new List<ChainedBlock>();

                foreach (var block in this.Values)
                {
                    if (block.TotalWork > maxTotalWork)
                    {
                        maxTotalWorkBlocks = new List<ChainedBlock>();
                        maxTotalWorkBlocks.Add(block);
                    }
                    else if (block.TotalWork == maxTotalWork)
                    {
                        maxTotalWorkBlocks.Add(block);
                    }
                }

                return maxTotalWorkBlocks;
            }
            catch (InvalidOperationException)
            {
                return Enumerable.Empty<ChainedBlock>();
            }
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