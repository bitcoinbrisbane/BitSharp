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
    public class InventoryPayload
    {
        public readonly ImmutableList<InventoryVector> InventoryVectors;

        public InventoryPayload(ImmutableList<InventoryVector> InventoryVectors)
        {
            this.InventoryVectors = InventoryVectors;
        }

        public InventoryPayload With(ImmutableList<InventoryVector> InventoryVectors = null)
        {
            return new InventoryPayload
            (
                InventoryVectors ?? this.InventoryVectors
            );
        }
    }
}
