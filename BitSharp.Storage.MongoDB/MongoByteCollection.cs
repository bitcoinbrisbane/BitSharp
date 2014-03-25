using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.MongoDB
{
    public class MongoByteCollection : IEnumerable<KeyValuePair<byte[], byte[]>>
    {
        private readonly MongoDatabase database;
        private readonly string name;
        private readonly MongoCollection<MongoKeyValuePair> collection;

        public MongoByteCollection(MongoDatabase database, string name)
        {
            this.database = database;
            this.name = name;
            this.collection = database.GetCollection<MongoKeyValuePair>(name);
            //this.collection.Settings.AssignIdOnInsert = false;
        }

        public int Count
        {
            get { return this.collection.Count().ToIntChecked(); }
        }

        public IEnumerable<byte[]> Keys
        {
            get
            {
                foreach (var x in this.collection.FindAll().SetFields("Id"))
                {
                    yield return x._id.Bytes;
                }
            }
        }

        public IEnumerable<byte[]> Values
        {
            get
            {
                foreach (var x in this.collection.FindAll())
                {
                    yield return x.Value.Bytes;
                }
            }
        }

        public bool ContainsKey(byte[] key)
        {
            return this.collection.Find(Query<MongoKeyValuePair>.EQ(x => x._id, key)).SetLimit(1).Count() > 0;
        }

        public bool TryGetValue(byte[] key, out byte[] value)
        {
            var mongoValue = this.collection.FindOneById(key);
            if (mongoValue != null)
            {
                value = mongoValue.Value.Bytes;
                return true;
            }
            else
            {
                value = default(byte[]);
                return false;
            }
        }

        public bool TryAdd(byte[] key, byte[] value)
        {
            try
            {
                if (!this.ContainsKey(key))
                {
                    this.collection.Insert(new MongoKeyValuePair { _id = key, Value = value });
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (WriteConcernException e)
            {
                return false;
            }
        }

        public byte[] this[byte[] key]
        {
            get
            {
                byte[] value;
                if (this.TryGetValue(key, out value))
                    return value;
                else
                    throw new KeyNotFoundException();
            }
            set
            {
                this.collection.Update(
                    Query<MongoKeyValuePair>.EQ(x => x._id, key),
                    Update<MongoKeyValuePair>.Replace(new MongoKeyValuePair { _id = key, Value = value }),
                    new MongoUpdateOptions { Flags = UpdateFlags.Upsert });
            }
        }

        public void Dispose()
        {
            //this.collection.Dispose();
        }

        public IEnumerator<KeyValuePair<byte[], byte[]>> GetEnumerator()
        {
            foreach (var x in this.collection.FindAll())
            {
                yield return new KeyValuePair<byte[], byte[]>(x._id.Bytes, x.Value.Bytes);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
