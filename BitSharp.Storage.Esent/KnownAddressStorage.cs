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
            return this.dict.ContainsKey(EncodeKey(networkAddressKey));
        }

        public bool TryGetValue(NetworkAddressKey networkAddressKey, out NetworkAddressWithTime networkAddressWithTime)
        {
            byte[] networkAddressWithTimeBytes;
            if (this.dict.TryGetValue(EncodeKey(networkAddressKey), out networkAddressWithTimeBytes))
            {
                networkAddressWithTime = DecodeValue(networkAddressWithTimeBytes);
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
            if (!this.ContainsKey(networkAddressKey))
            {
                try
                {
                    this.dict.Add(EncodeKey(networkAddressKey), EncodeValue(networkAddressWithTime));
                    return true;
                }
                catch (ArgumentException)
                { return false; }
            }
            else
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
                this.dict[EncodeKey(networkAddressKey).ToArray()] = EncodeValue(value);
            }
        }

        public IEnumerator<KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>> GetEnumerator()
        {
            foreach (var keyPair in this.dict)
                yield return new KeyValuePair<NetworkAddressKey, NetworkAddressWithTime>(DecodeKey(keyPair.Key), DecodeValue(keyPair.Value));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private static byte[] EncodeKey(NetworkAddressKey networkAddressKey)
        {
            return NetworkEncoder.EncodeNetworkAddressKey(networkAddressKey);
        }

        private static byte[] EncodeValue(NetworkAddressWithTime networkAddressWithTime)
        {
            return NetworkEncoder.EncodeNetworkAddressWithTime(networkAddressWithTime);
        }

        private static NetworkAddressKey DecodeKey(byte[] networkAddressKeyBytes)
        {
            return NetworkEncoder.DecodeNetworkAddressKey(networkAddressKeyBytes.ToMemoryStream());
        }

        private static NetworkAddressWithTime DecodeValue(byte[] networkAddressWithTimeBytes)
        {
            return NetworkEncoder.DecodeNetworkAddressWithTime(networkAddressWithTimeBytes.ToMemoryStream());
        }
    }
}
