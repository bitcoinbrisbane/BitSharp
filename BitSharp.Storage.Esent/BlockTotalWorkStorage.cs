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
using System.Numerics;

namespace BitSharp.Storage.Esent
{
    public class BlockTotalWorkStorage : EsentDataStorage, IBlockTotalWorkStorage
    {
        public BlockTotalWorkStorage(EsentStorageContext storageContext)
            : base(storageContext, "blocksTotalWork")
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.Data.Keys;
        }

        public IEnumerable<KeyValuePair<UInt256, BigInteger>> ReadAllValues()
        {
            return this.Data.Select(x =>
                new KeyValuePair<UInt256, BigInteger>(x.Key, StorageEncoder.DecodeTotalWork(x.Value.ToMemoryStream())));
        }

        public bool TryReadValue(UInt256 blockHash, out BigInteger totalWork)
        {
            byte[] totalWorkBytes;
            if (this.Data.TryGetValue(blockHash, out totalWorkBytes))
            {
                totalWork = StorageEncoder.DecodeTotalWork(totalWorkBytes.ToMemoryStream());
                return true;
            }
            else
            {
                totalWork = default(BigInteger);
                return false;
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<BigInteger>>> keyPairs)
        {
            foreach (var keyPair in keyPairs)
                this.Data[keyPair.Key] = StorageEncoder.EncodeTotalWork(keyPair.Value.Value);

            return true;
        }

        public void Truncate()
        {
            this.Data.Clear();
        }

        public IEnumerable<UInt256> SelectMaxTotalWorkBlocks()
        {
            var maxTotalWork = this.Data.PersistentDictionary.Values.Max();
            return this.Data.PersistentDictionary.Where(x => x.Value == maxTotalWork)
                .Select(x => new UInt256(Convert.FromBase64String(x.Key)));
        }
    }
}
