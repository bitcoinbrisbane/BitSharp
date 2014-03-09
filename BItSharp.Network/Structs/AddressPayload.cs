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
    public class AddressPayload
    {
        public readonly ImmutableList<NetworkAddressWithTime> NetworkAddresses;

        public AddressPayload(ImmutableList<NetworkAddressWithTime> NetworkAddresses)
        {
            this.NetworkAddresses = NetworkAddresses;
        }

        public AddressPayload With(ImmutableList<NetworkAddressWithTime> NetworkAddresses = null)
        {
            return new AddressPayload
            (
                NetworkAddresses ?? this.NetworkAddresses
            );
        }
    }
}
