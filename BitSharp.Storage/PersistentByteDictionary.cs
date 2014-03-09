//#define BYTE_ARRAY_SUPPORTED

using Microsoft.Isam.Esent.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class PersistentByteDictionary : IDictionary<byte[], byte[]>, IDisposable
    {

#if BYTE_ARRAY_SUPPORTED
        private PersistentDictionary<byte[], byte[]> dict;
#else
        private PersistentDictionary<string, string> dict;
#endif
        public PersistentByteDictionary(string directory)
        {
#if BYTE_ARRAY_SUPPORTED
            this.dict = new PersistentDictionary<byte[], byte[]>();
#else
            this.dict = new PersistentDictionary<string, string>(directory);
#endif
        }

        public PersistentByteDictionary(IEnumerable<KeyValuePair<byte[], byte[]>> dictionary, string directory)
            : this(directory)
        {
            foreach (var item in dictionary)
            {
                this.Add(item);
            }
        }
        public void Add(byte[] key, byte[] value)
        {
            this.dict.Add(Encode(key), Encode(value));
        }

        public bool ContainsKey(byte[] key)
        {
            return this.dict.ContainsKey(Encode(key));
        }

        public ICollection<byte[]> Keys
        {
            get { return this.dict.Keys.Select(x => Decode(x)).ToList(); }
        }

        public bool Remove(byte[] key)
        {
            return this.dict.Remove(Encode(key));
        }

        public bool TryGetValue(byte[] key, out byte[] value)
        {
            string s;
            if (this.dict.TryGetValue(Encode(key), out s))
            {
                value = Decode(s);
                return true;
            }
            else
            {
                value = default(byte[]);
                return false;
            }
        }

        public ICollection<byte[]> Values
        {
            get { return this.dict.Values.Select(x => Decode(x)).ToList(); }
        }

        public byte[] this[byte[] key]
        {
            get
            {
                return Decode(this.dict[Encode(key)]);
            }
            set
            {
                this.dict[Encode(key)] = Encode(value);
            }
        }

        public void Add(KeyValuePair<byte[], byte[]> item)
        {
            this.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            this.dict.Clear();
        }

        public bool Contains(KeyValuePair<byte[], byte[]> item)
        {
            //TODO not sure what the override semantics should be on Contains(KeyValuePair)
            return this.ContainsKey(item.Key);
        }

        public void CopyTo(KeyValuePair<byte[], byte[]>[] array, int arrayIndex)
        {
            foreach (var keyPair in this)
            {
                array[arrayIndex] = new KeyValuePair<byte[], byte[]>(keyPair.Key, keyPair.Value);
                arrayIndex++;
            }
        }

        public int Count
        {
            get { return this.dict.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<byte[], byte[]> item)
        {
            return this.Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<byte[], byte[]>> GetEnumerator()
        {
            foreach (var item in
                this.dict.Select(x => new KeyValuePair<byte[], byte[]>(Decode(x.Key), Decode(x.Value))))
            {
                yield return item;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Dispose()
        {
            this.dict.Dispose();
        }

        private static string Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        private static byte[] Decode(string s)
        {
            return Convert.FromBase64String(s);
        }
    }
}
