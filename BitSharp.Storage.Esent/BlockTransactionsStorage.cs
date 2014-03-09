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
using System.Collections.Immutable;
using System.IO;

namespace BitSharp.Storage.Esent
{
    public class BlockTransactionsStorage : EsentDataStorage, IBlockTransactionsStorage
    {
        public BlockTransactionsStorage(EsentStorageContext storageContext)
            : base(storageContext, "blockTxHashes")
        { }

        public IEnumerable<UInt256> ReadAllKeys()
        {
            return this.ReadAllDataKeys().Select(x => new UInt256(x));
        }

        public IEnumerable<KeyValuePair<UInt256, ImmutableList<UInt256>>> ReadAllValues()
        {
            return this.ReadAllDataValues().Select(x =>
                {
                    var blockHash = new UInt256(x.Key);
                    var txHashesBytes = x.Value;

                    var txHashes = ImmutableList.CreateBuilder<UInt256>();
                    var txHashBytes = new byte[32];
                    for (var i = 0; i < txHashesBytes.Length; i += 32)
                    {
                        Array.Copy(txHashesBytes, i, txHashBytes, 0, 32);
                        txHashes.Add(new UInt256(txHashBytes));
                    }

                    return new KeyValuePair<UInt256, ImmutableList<UInt256>>(blockHash, txHashes.ToImmutable());
                });
        }

        public bool TryReadValue(UInt256 blockHash, out ImmutableList<UInt256> blockTxHashes)
        {
            byte[] txHashesBytes;
            if (this.TryReadDataValue(blockHash.ToByteArray(), out txHashesBytes))
            {
                var txHashes = ImmutableList.CreateBuilder<UInt256>();
                var txHashBytes = new byte[32];
                for (var i = 0; i < txHashesBytes.Length; i += 32)
                {
                    Array.Copy(txHashesBytes, i, txHashBytes, 0, 32);
                    txHashes.Add(new UInt256(txHashBytes));
                }

                blockTxHashes = txHashes.ToImmutable();
                return true;
            }
            else
            {
                blockTxHashes = default(ImmutableList<UInt256>);
                return false;
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<UInt256, WriteValue<ImmutableList<UInt256>>>> keyPairs)
        {
            return this.TryWriteDataValues(keyPairs.Select(x =>
                {
                    var txHashesBytes = new byte[x.Value.Value.Count * 32];
                    for (var i = 0; i < x.Value.Value.Count; i++)
                    {
                        Array.Copy(x.Value.Value[i].ToByteArray(), 0, txHashesBytes, i * 32, 32);
                    }

                    return new KeyValuePair<byte[], WriteValue<byte[]>>(x.Key.ToByteArray(), new WriteValue<byte[]>(txHashesBytes, x.Value.IsCreate));
                }));
        }

        public void Truncate()
        {
            this.TruncateData();
        }
    }
}
