using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage.MongoDB
{
    internal class MongoKeyValuePair
    {
        public BsonBinaryData _id { get; set; }
        public BsonBinaryData Value { get; set; }
    }
}
