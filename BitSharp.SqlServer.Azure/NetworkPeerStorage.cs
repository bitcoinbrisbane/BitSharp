using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Node.Storage;

namespace BitSharp.SqlServer.Azure
{
    public class NetworkPeerStorage : INetworkPeerStorage
    {
        //hack:  use collection
        private Dictionary<Node.Domain.NetworkAddressKey, Node.Domain.NetworkAddressWithTime> db = new Dictionary<Node.Domain.NetworkAddressKey, Node.Domain.NetworkAddressWithTime>();

        public int Count
        {
            get { return db.Count; }
        }

        public IEnumerable<Node.Domain.NetworkAddressKey> Keys
        {
            get { return db.Keys; }
        }

        public IEnumerable<Node.Domain.NetworkAddressWithTime> Values
        {
            get { return db.Values; }
        }

        public bool ContainsKey(Node.Domain.NetworkAddressKey key)
        {
            return db.ContainsKey(key);
        }

        public bool TryGetValue(Node.Domain.NetworkAddressKey key, out Node.Domain.NetworkAddressWithTime value)
        {
            throw new NotImplementedException();
        }

        public bool TryAdd(Node.Domain.NetworkAddressKey key, Node.Domain.NetworkAddressWithTime value)
        {
            throw new NotImplementedException();
        }

        public bool TryRemove(Node.Domain.NetworkAddressKey key)
        {
            throw new NotImplementedException();
        }

        public Node.Domain.NetworkAddressWithTime this[Node.Domain.NetworkAddressKey key]
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }

        public IEnumerator<KeyValuePair<Node.Domain.NetworkAddressKey, Node.Domain.NetworkAddressWithTime>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
