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
        private readonly EsentStorageContext storageContext;
        private readonly string name;
        private readonly string directory;
        private readonly PersistentByteDictionary dict;

        public KnownAddressStorage(EsentStorageContext storageContext)
        {
            this.storageContext = storageContext;
            this.name = "knownAddresses";
            this.directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BitSharp", "peers", this.name);
            this.dict = new PersistentByteDictionary(this.directory);
        }

        public void Dispose()
        {
            this.dict.Dispose();
        }

        public EsentStorageContext StorageContext { get { return this.storageContext; } }

        public int Count
        {
            get { return this.dict.Count; }
        }

        public IEnumerable<NetworkAddressKey> Keys
        {
            get { return this.Select(x => x.Key); }
        }

        public IEnumerable<NetworkAddressWithTime> Values
        {
            get { return this.Select(x => x.Value); }
        }

        public bool ContainsKey(NetworkAddressKey networkAddressKey)
        {
            return this.dict.ContainsKey(NetworkEncoder.EncodeNetworkAddressKey(networkAddressKey));
        }

        public bool TryGetValue(NetworkAddressKey networkAddressKey, out NetworkAddressWithTime networkAddressWithTime)
        {
            byte[] networkAddressWithTimeBytes;
            if (this.dict.TryGetValue(NetworkEncoder.EncodeNetworkAddressKey(networkAddressKey), out networkAddressWithTimeBytes))
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

        public bool TryAdd(NetworkAddressKey networkAddressKey, NetworkAddressWithTime networkAddressWithTime)
        {
            try
            {
                this.dict.Add(NetworkEncoder.EncodeNetworkAddressKey(networkAddressKey).ToArray(), NetworkEncoder.EncodeNetworkAddressWithTime(networkAddressWithTime));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public NetworkAddressWithTime this[NetworkAddressKey networkAddressKey]
        {
            get
            {
                NetworkAddressWithTime networkAddressWithTime;
                if (this.TryGetValue(networkAddressKey, out networkAddressWithTime))
                    return networkAddressWithTime;

                throw new KeyNotFoundException();
            }
            set
            {
                this.dict[NetworkEncoder.EncodeNetworkAddressKey(networkAddressKey).ToArray()] = NetworkEncoder.EncodeNetworkAddressWithTime(value);
            }
        }

        public IEnumerator<KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>> GetEnumerator()
        {
            foreach (var keyPair in this.dict)
                yield return new KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>(
                    NetworkEncoder.DecodeNetworkAddressKey(keyPair.Key.ToMemoryStream()),
                    NetworkEncoder.DecodeNetworkAddressWithTime(keyPair.Value.ToMemoryStream()));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
