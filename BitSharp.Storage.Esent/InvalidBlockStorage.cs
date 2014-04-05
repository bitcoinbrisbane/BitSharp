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
    public class InvalidBlockStorage : EsentDataStorage<string>, IInvalidBlockStorage
    {
        public InvalidBlockStorage(string baseDirectory)
            : base(baseDirectory, "invalidBlocks",
                data => DataEncoder.EncodeVarString(data),
                (blockHash, bytes) => DataEncoder.DecodeVarString(bytes))
        { }
    }
}
