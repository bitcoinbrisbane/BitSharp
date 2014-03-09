using BitSharp.Common;
using BitSharp.Network.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BitSharp.Network
{
    public class GetBlocksPayload
    {
        public readonly UInt32 Version;
        public readonly ImmutableList<UInt256> BlockLocatorHashes;
        public readonly UInt256 HashStop;

        public GetBlocksPayload(UInt32 Version, ImmutableList<UInt256> BlockLocatorHashes, UInt256 HashStop)
        {
            this.Version = Version;
            this.BlockLocatorHashes = BlockLocatorHashes;
            this.HashStop = HashStop;
        }

        public GetBlocksPayload With(UInt32? Version = null, ImmutableList<UInt256> BlockLocatorHashes = null, UInt256? HashStop = null)
        {
            return new GetBlocksPayload
            (
                Version ?? this.Version,
                BlockLocatorHashes ?? this.BlockLocatorHashes,
                HashStop ?? this.HashStop
            );
        }
    }
}
