using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using NLog;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Script
{
    public class PayToPublicKeyBuilder
    {
        public byte[] CreateOutput(byte[] publicKey)
        {
            using (var outputScript = new ScriptBuilder())
            {
                outputScript.WritePushData(publicKey);
                outputScript.WriteOp(ScriptOp.OP_CHECKSIG);

                return outputScript.GetScript();
            }
        }
    }
}
