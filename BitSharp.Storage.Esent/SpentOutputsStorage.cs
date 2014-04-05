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
    public class SpentOutputsStorage : EsentDataStorage<IImmutableList<KeyValuePair<TxOutputKey, TxOutput>>>, ISpentOutputsStorage
    {
        public SpentOutputsStorage(string baseDirectory)
            : base(baseDirectory, "spentOutputs",
                keyPairs =>
                {
                    using (var stream = new MemoryStream())
                    {
                        foreach (var keyPair in keyPairs)
                        {
                            DataEncoder.EncodeTxOutputKey(stream, keyPair.Key);
                            DataEncoder.EncodeTxOutput(stream, keyPair.Value);
                        }

                        return stream.ToArray();
                    }
                },
                (blockHash, bytes) =>
                {
                    using (var stream = new MemoryStream(bytes))
                    {
                        var keyPairs = ImmutableList.CreateBuilder<KeyValuePair<TxOutputKey, TxOutput>>();
                        while (stream.Position < stream.Length)
                        {
                            var txOutputKey = DataEncoder.DecodeTxOutputKey(stream);
                            var txOutput = DataEncoder.DecodeTxOutput(stream);
                            keyPairs.Add(new KeyValuePair<TxOutputKey, TxOutput>(txOutputKey, txOutput));
                        }
                        
                        return keyPairs.ToImmutable();
                    }
                })
        { }
    }
}
