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

namespace BitSharp.Storage.MongoDB
{
    public class BlockTxHashesStorage : MongoDBDataStorage<IImmutableList<UInt256>>
    {
        public BlockTxHashesStorage(MongoDBStorageContext storageContext)
            : base(storageContext, "blockTxHashes",
                txHashes =>
                {
                    var txHashesBytes = new byte[txHashes.Count * 32];
                    for (var i = 0; i < txHashes.Count; i++)
                    {
                        Buffer.BlockCopy(txHashes[i].ToByteArray(), 0, txHashesBytes, i * 32, 32);
                    }

                    return txHashesBytes;
                },
                (blockHash, bytes) =>
                {
                    var txHashes = ImmutableList.CreateBuilder<UInt256>();
                    var txHashBytes = new byte[32];
                    for (var i = 0; i < bytes.Length; i += 32)
                    {
                        Buffer.BlockCopy(bytes, i, txHashBytes, 0, 32);
                        txHashes.Add(new UInt256(txHashBytes));
                    }

                    return txHashes.ToImmutable();
                })
        { }
    }
}
