using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Collections.Immutable;
using System.IO;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using BitSharp.Core;

namespace BitSharp.Esent
{
    public class SpentTransactionsStorage : EsentDataStorage<IImmutableList<KeyValuePair<UInt256, SpentTx>>>, ISpentTransactionsStorage
    {
        public SpentTransactionsStorage(string baseDirectory)
            : base(baseDirectory, "spentTransactions",
                keyPairs =>
                {
                    using (var stream = new MemoryStream())
                    {
                        foreach (var keyPair in keyPairs)
                        {
                            DataEncoder.EncodeUInt256(stream, keyPair.Key);
                            DataEncoder.EncodeSpentTx(stream, keyPair.Value);
                        }

                        return stream.ToArray();
                    }
                },
                (blockHash, bytes) =>
                {
                    using (var stream = new MemoryStream(bytes))
                    {
                        var keyPairs = ImmutableList.CreateBuilder<KeyValuePair<UInt256, SpentTx>>();

                        while (stream.Position < stream.Length)
                        {
                            var txHash = DataEncoder.DecodeUInt256(stream);
                            var spentTx = DataEncoder.DecodeSpentTx(stream);

                            keyPairs.Add(new KeyValuePair<UInt256, SpentTx>(txHash, spentTx));
                        }

                        return keyPairs.ToImmutable();
                    }
                })
        { }
    }
}
