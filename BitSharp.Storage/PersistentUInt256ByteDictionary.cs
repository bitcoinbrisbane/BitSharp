//#define BYTE_ARRAY_SUPPORTED

using Microsoft.Isam.Esent.Collections.Generic;
using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitSharp.Common;
using System.Linq.Expressions;

namespace BitSharp.Storage
{
    public class PersistentUInt256ByteDictionary : IDictionary<UInt256, byte[]>, IDisposable
    {

#if BYTE_ARRAY_SUPPORTED
        private PersistentDictionary<string, byte[]> dict;
#else
        private PersistentDictionary<string, string> dict;
#endif
        public PersistentUInt256ByteDictionary(string directory)
        {
#if BYTE_ARRAY_SUPPORTED
            this.dict = new PersistentDictionary<string, byte[]>(directory);
#else
            this.dict = new PersistentDictionary<string, string>(directory);
#endif
        }

        public PersistentUInt256ByteDictionary(IEnumerable<KeyValuePair<UInt256, byte[]>> dictionary, string directory)
            : this(directory)
        {
            foreach (var item in dictionary)
            {
                this.Add(item);
            }
        }

        ~PersistentUInt256ByteDictionary()
        {
            this.Dispose();
        }

        public PersistentDictionary<string, string> PersistentDictionary { get { return this.dict; } }

        public void Add(UInt256 key, byte[] value)
        {
#if BYTE_ARRAY_SUPPORTED
            this.dict.Add(Encode(key), value);
#else
            this.dict.Add(Encode(key), Encode(value));
#endif
        }

        public bool ContainsKey(UInt256 key)
        {
            return this.dict.ContainsKey(Encode(key));
        }

        public ICollection<UInt256> Keys
        {
#if BYTE_ARRAY_SUPPORTED
            get { return new PersistentUInt256ByteDictionaryKeyCollection(this.dict.Keys); }
#else
            get { return new PersistentUInt256ByteDictionaryKeyCollection(this.dict.Keys); }
#endif
        }

        public bool Remove(UInt256 key)
        {
            return this.dict.Remove(Encode(key));
        }

        public bool TryGetValue(UInt256 key, out byte[] value)
        {
#if BYTE_ARRAY_SUPPORTED
            return this.dict.TryGetValue(Encode(key), out value);
#else
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
#endif
        }

        public ICollection<byte[]> Values
        {
#if BYTE_ARRAY_SUPPORTED
            get { return this.dict.Values; }
#else
            get { return new PersistentUInt256ByteDictionaryValueCollection(this.dict.Values); }
#endif
        }

        public byte[] this[UInt256 key]
        {
            get
            {
#if BYTE_ARRAY_SUPPORTED
                return this.dict[Encode(key)];
#else
                return Decode(this.dict[Encode(key)]);
#endif
            }
            set
            {
#if BYTE_ARRAY_SUPPORTED
                this.dict[Encode(key)] = value;
#else
                this.dict[Encode(key)] = Encode(value);
#endif
            }
        }

        public void Add(KeyValuePair<UInt256, byte[]> item)
        {
            this.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            this.dict.Clear();
        }

        public bool Contains(KeyValuePair<UInt256, byte[]> item)
        {
            //TODO not sure what the override semantics should be on Contains(KeyValuePair)
            return this.ContainsKey(item.Key);
        }

        public void CopyTo(KeyValuePair<UInt256, byte[]>[] array, int arrayIndex)
        {
            foreach (var keyPair in this)
            {
                array[arrayIndex] = new KeyValuePair<UInt256, byte[]>(keyPair.Key, keyPair.Value);
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

        public bool Remove(KeyValuePair<UInt256, byte[]> item)
        {
            return this.Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<UInt256, byte[]>> GetEnumerator()
        {
#if BYTE_ARRAY_SUPPORTED
            foreach (var item in
                this.dict.Select(x => new KeyValuePair<UInt256, byte[]>(Decode(x.Key), x.Value)))
            {
                yield return item;
            }
#else
            foreach (var item in
                this.dict.Select(x => new KeyValuePair<UInt256, byte[]>(DecodeUInt256(x.Key), Decode(x.Value))))
            {
                yield return item;
            }
#endif
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

        private static string Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        private static string Encode(UInt256 value)
        {
            return Encode(value.ToByteArray());
        }

        private static byte[] Decode(string s)
        {
            return Convert.FromBase64String(s);
        }

        private static UInt256 DecodeUInt256(string s)
        {
            return new UInt256(Decode(s));
        }

        public class PersistentUInt256ByteDictionaryKeyCollection : ICollection<UInt256>
        {
#if BYTE_ARRAY_SUPPORTED
            PersistentDictionaryKeyCollection<string, byte[]> keyCollection;
#else
            PersistentDictionaryKeyCollection<string, string> keyCollection;
#endif

#if BYTE_ARRAY_SUPPORTED
            public PersistentUInt256ByteDictionaryKeyCollection(PersistentDictionaryKeyCollection<string, byte[]> keyCollection)
#else
            public PersistentUInt256ByteDictionaryKeyCollection(PersistentDictionaryKeyCollection<string, string> keyCollection)
#endif
            {
                this.keyCollection = keyCollection;
            }

            public void Add(UInt256 item)
            {
                this.keyCollection.Add(Encode(item));
            }

            public void Clear()
            {
                this.keyCollection.Clear();
            }

            public bool Contains(UInt256 item)
            {
                return this.keyCollection.Contains(Encode(item));
            }

            public void CopyTo(UInt256[] array, int arrayIndex)
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

            public bool Remove(UInt256 item)
            {
                return this.keyCollection.Remove(Encode(item));
            }

            public IEnumerator<UInt256> GetEnumerator()
            {
                foreach (var key in this.keyCollection)
                    yield return DecodeUInt256(key);

            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        public class PersistentUInt256ByteDictionaryValueCollection : ICollection<byte[]>
        {
#if BYTE_ARRAY_SUPPORTED
            PersistentDictionaryValueCollection<string, byte[]> valueCollection;
#else
            PersistentDictionaryValueCollection<string, string> valueCollection;
#endif

#if BYTE_ARRAY_SUPPORTED
            public PersistentUInt256ByteDictionaryValueCollection(PersistentDictionaryValueCollection<string, byte[]> valueCollection)
#else
            public PersistentUInt256ByteDictionaryValueCollection(PersistentDictionaryValueCollection<string, string> valueCollection)
#endif
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
