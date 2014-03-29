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
    public class BlockRollbackStorage : EsentDataStorage<IImmutableList<KeyValuePair<UInt256, UInt256>>>, IBlockRollbackStorage
    {
        public BlockRollbackStorage(EsentStorageContext storageContext)
            : base(storageContext, "blockRollback",
                keyPairs =>
                {
                    var bytes = new byte[keyPairs.Count * 32 * 2];
                    for (var i = 0; i < keyPairs.Count; i++)
                    {
                        Buffer.BlockCopy(keyPairs[i].Key.ToByteArray(), 0, bytes, i * 64, 32);
                        Buffer.BlockCopy(keyPairs[i].Value.ToByteArray(), 0, bytes, i * 64 + 32, 32);
                    }

                    return bytes;
                },
                (blockHash, bytes) =>
                {
                    var keyPairs = ImmutableList.CreateBuilder<KeyValuePair<UInt256, UInt256>>();
                    var txHashBytes = new byte[32];
                    var blockHashBytes = new byte[32];
                    for (var i = 0; i < bytes.Length; i += 64)
                    {
                        Buffer.BlockCopy(bytes, i, txHashBytes, 0, 32);
                        Buffer.BlockCopy(bytes, i + 32, blockHashBytes, 0, 32);
                        var keyPair = new KeyValuePair<UInt256, UInt256>(new UInt256(txHashBytes), new UInt256(blockHashBytes));
                    }

                    return keyPairs.ToImmutable();
                })
        { }
    }
}
