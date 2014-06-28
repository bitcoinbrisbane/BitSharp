using BitSharp.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Domain
{
    public class UtxoStream : Stream
    {
        private readonly IEnumerator<KeyValuePair<UInt256, UnspentTx>> unspentTransactions;
        private readonly List<byte> unreadBytes;

        public UtxoStream(IEnumerable<KeyValuePair<UInt256, UnspentTx>> unspentTransactions)
        {
            this.unspentTransactions = unspentTransactions.GetEnumerator();
            this.unreadBytes = new List<byte>();
        }

        protected override void Dispose(bool disposing)
        {
            this.unspentTransactions.Dispose();
            base.Dispose(disposing);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (
                unreadBytes.Count < count
                && this.unspentTransactions.MoveNext()
            )
            {
                var txHash = this.unspentTransactions.Current.Key;
                var unspentTx = this.unspentTransactions.Current.Value;

                unreadBytes.AddRange(txHash.ToByteArray());
                unreadBytes.AddRange(DataEncoder.EncodeUnspentTx(unspentTx));
            }

            var available = unreadBytes.Count;
            if (available >= count)
            {
                unreadBytes.CopyTo(0, buffer, offset, count);
                unreadBytes.RemoveRange(0, count);
                return count;
            }
            else
            {
                unreadBytes.CopyTo(0, buffer, offset, available);
                unreadBytes.Clear();
                return available;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
