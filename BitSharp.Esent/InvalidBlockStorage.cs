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
using BitSharp.Core.Storage;
using BitSharp.Core;

namespace BitSharp.Esent
{
    public class InvalidBlockStorage : EsentDataStorage<string>, IInvalidBlockStorage
    {
        public InvalidBlockStorage(string baseDirectory)
            : base(baseDirectory, "InvalidBlocks",
                data => DataEncoder.EncodeVarString(data),
                (blockHash, bytes) => DataEncoder.DecodeVarString(bytes))
        { }
    }
}
