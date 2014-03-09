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
    public class KnownAddressStorage : EsentDataStorage, IBoundedStorage<NetworkAddressKey, NetworkAddressWithTime>
    {
        public KnownAddressStorage(EsentStorageContext storageContext)
            : base(storageContext, "knownAddresses")
        { }

        public IEnumerable<NetworkAddressKey> ReadAllKeys()
        {
            return this.Data.Keys.Select(x => NetworkEncoder.DecodeNetworkAddressKey(x.ToMemoryStream()));
        }

        public IEnumerable<KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>> ReadAllValues()
        {
            return this.Data.Select(x =>
                new KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>(NetworkEncoder.DecodeNetworkAddressKey(x.Key.ToMemoryStream()), NetworkEncoder.DecodeNetworkAddressWithTime(x.Value.ToMemoryStream())));
        }

        public bool TryReadValue(NetworkAddressKey networkAddressKey, out NetworkAddressWithTime networkAddressWithTime)
        {
            byte[] networkAddressWithTimeBytes;
            if (this.Data.TryGetValue(NetworkEncoder.EncodeNetworkAddressKey(networkAddressKey), out networkAddressWithTimeBytes))
            {
                networkAddressWithTime = NetworkEncoder.DecodeNetworkAddressWithTime(networkAddressWithTimeBytes.ToMemoryStream());
                return true;
            }
            else
            {
                networkAddressWithTime = default(NetworkAddressWithTime);
                return false;
            }
        }

        public bool TryWriteValues(IEnumerable<KeyValuePair<NetworkAddressKey, WriteValue<NetworkAddressWithTime>>> keyPairs)
        {
            foreach (var keyPair in keyPairs)
                this.Data[NetworkEncoder.EncodeNetworkAddressKey(keyPair.Key).ToArray()] = NetworkEncoder.EncodeNetworkAddressWithTime(keyPair.Value.Value);

            return true;
        }

        public void Truncate()
        {
            this.Data.Clear();
        }
    }
}
