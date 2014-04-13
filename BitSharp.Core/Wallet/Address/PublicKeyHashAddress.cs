﻿using BitSharp.Common;
using BitSharp.Core.Script;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Wallet.Address
{
    public class PublicKeyHashAddress : IWalletAddress
    {
        private ImmutableArray<byte> publicKeyHashBytes;
        private UInt256 outputScriptHash;

        public PublicKeyHashAddress(ImmutableArray<byte> publicKeyHashBytes)
        {
            this.publicKeyHashBytes = publicKeyHashBytes;

            var sha256 = new SHA256Managed();
            var outputScript = new PayToPublicKeyHashBuilder().CreateOutputFromPublicKeyHash(publicKeyHashBytes.ToArray());
            this.outputScriptHash = new UInt256(sha256.ComputeHash(outputScript));
        }

        public IEnumerable<UInt256> GetOutputScriptHashes()
        {
            yield return this.outputScriptHash;
        }

        public bool IsMatcher
        {
            get { return false; }
        }

        public bool MatchesTxOutput(Domain.TxOutput txOutput, UInt256 txOutputScriptHash)
        {
            throw new NotSupportedException();
        }
    }
}
