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
using System.IO;

namespace BitSharp.Storage.Esent
{
    public class KnownAddressStorage : IBoundedStorage<NetworkAddressKey, NetworkAddressWithTime>
    {
        private readonly EsentStorageContext _storageContext;
        private readonly string _name;
        private readonly string _dataPath;
        private readonly PersistentByteDictionary _data;

        public KnownAddressStorage(EsentStorageContext storageContext)
        {
            this._storageContext = storageContext;
            this._name = "knownAddresses";
            this._dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "data", this._name);
            this._data = new PersistentByteDictionary(this._dataPath);
        }

        public void Dispose()
        {
            this._data.Dispose();
        }

        public EsentStorageContext StorageContext { get { return this._storageContext; } }

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

        protected PersistentByteDictionary Data { get { return this._data; } }
    }
}
