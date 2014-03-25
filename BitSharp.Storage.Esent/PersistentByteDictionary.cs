using Microsoft.Isam.Esent.Collections.Generic;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.Esent
{
    public class PersistentByteDictionary : IDictionary<byte[], byte[]>, IDisposable
    {
        private PersistentDictionary<string, string> dict;

        public PersistentByteDictionary(string directory)
        {
            this.dict = new PersistentDictionary<string, string>(directory);
        }

        public PersistentByteDictionary(IEnumerable<KeyValuePair<byte[], byte[]>> dictionary, string directory)
            : this(directory)
        {
            foreach (var item in dictionary)
            {
                this.Add(item);
            }
        }

        ~PersistentByteDictionary()
        {
            this.Dispose();
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
            get { return new PersistentByteDictionaryKeyCollection(this.dict.Keys); }
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
            get { return new PersistentByteDictionaryValueCollection(this.dict.Values); }
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
            GC.SuppressFinalize(this);
        }

        public void Flush()
        {
            this.dict.Flush();
        }

        private static string Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        private static byte[] Decode(string s)
        {
            return Convert.FromBase64String(s);
        }

        public class PersistentByteDictionaryKeyCollection : ICollection<byte[]>
        {
            PersistentDictionaryKeyCollection<string, string> keyCollection;

            public PersistentByteDictionaryKeyCollection(PersistentDictionaryKeyCollection<string, string> keyCollection)
            {
                this.keyCollection = keyCollection;
            }

            public void Add(byte[] item)
            {
                this.keyCollection.Add(Encode(item));
            }

            public void Clear()
            {
                this.keyCollection.Clear();
            }

            public bool Contains(byte[] item)
            {
                return this.keyCollection.Contains(Encode(item));
            }

            public void CopyTo(byte[][] array, int arrayIndex)
            {
                foreach (var key in this)
                {
                    array[arrayIndex] = key;
                    arrayIndex++;
                }
            }

            public int Count
            {
                get { return this.keyCollection.Count; }
            }

            public bool IsReadOnly
            {
                get { return this.keyCollection.IsReadOnly; }
            }

            public bool Remove(byte[] item)
            {
                return this.keyCollection.Remove(Encode(item));
            }

            public IEnumerator<byte[]> GetEnumerator()
            {
                foreach (var key in this.keyCollection)
                    yield return Decode(key);

            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        public class PersistentByteDictionaryValueCollection : ICollection<byte[]>
        {
            PersistentDictionaryValueCollection<string, string> valueCollection;

            public PersistentByteDictionaryValueCollection(PersistentDictionaryValueCollection<string, string> valueCollection)
            {
                this.valueCollection = valueCollection;
            }

            public void Add(byte[] item)
            {
                this.valueCollection.Add(Encode(item));
            }

            public void Clear()
            {
                this.valueCollection.Clear();
            }

            public bool Contains(byte[] item)
            {
                return this.valueCollection.Contains(Encode(item));
            }

            public void CopyTo(byte[][] array, int arrayIndex)
            {
                foreach (var value in this)
                {
                    array[arrayIndex] = value;
                    arrayIndex++;
                }
            }

            public int Count
            {
                get { return this.valueCollection.Count; }
            }

            public bool IsReadOnly
            {
                get { return this.valueCollection.IsReadOnly; }
            }

            public bool Remove(byte[] item)
            {
                return this.valueCollection.Remove(Encode(item));
            }

            public IEnumerator<byte[]> GetEnumerator()
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                foreach (var value in this.valueCollection)
                    yield return Decode(value);

                stopwatch.Stop();
                Debug.WriteLine(stopwatch.ElapsedSecondsFloat());
                Debugger.Break();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}
