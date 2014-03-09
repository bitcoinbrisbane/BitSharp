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
            return this.ReadAllDataKeys().Select(x => NetworkEncoder.DecodeNetworkAddressKey(x.ToMemoryStream()));
        }

        public IEnumerable<KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>> ReadAllValues()
        {
            return this.ReadAllDataValues().Select(x =>
                new KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>(NetworkEncoder.DecodeNetworkAddressKey(x.Key.ToMemoryStream()), NetworkEncoder.DecodeNetworkAddressWithTime(x.Value.ToMemoryStream())));
        }

        public bool TryReadValue(NetworkAddressKey networkAddressKey, out NetworkAddressWithTime networkAddressWithTime)
        {
            byte[] networkAddressWithTimeBytes;
            if (this.TryReadDataValue(NetworkEncoder.EncodeNetworkAddressKey(networkAddressKey), out networkAddressWithTimeBytes))
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
            return this.TryWriteDataValues(keyPairs.Select(x => new KeyValuePair<byte[], WriteValue<byte[]>>(NetworkEncoder.EncodeNetworkAddressKey(x.Key).ToArray(), new WriteValue<byte[]>(NetworkEncoder.EncodeNetworkAddressWithTime(x.Value.Value), x.Value.IsCreate))));
        }

        public void Truncate()
        {
            this.TruncateData();
        }
    }
}
