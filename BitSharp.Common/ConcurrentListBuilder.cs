using BitSharp.Common.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public class ConcurrentListBuilder<T> : IEnumerable<T>, IList<T>, ICollection<T>
    {
        private readonly ImmutableList<T>.Builder builder;
        //TODO make the set disposable because of lock?
        private readonly ReaderWriterLockSlim builderLock;

        public ConcurrentListBuilder()
        {
            this.builder = ImmutableList.CreateBuilder<T>();
            this.builderLock = new ReaderWriterLockSlim();
        }

        public ConcurrentListBuilder(ImmutableList<T> list)
        {
            this.builder = list.ToBuilder();
            this.builderLock = new ReaderWriterLockSlim();
        }

        public void Add(T item)
        {
            this.builderLock.DoWrite(() =>
                this.builder.Add(item));
        }

        public void Clear()
        {
            this.builderLock.DoWrite(() =>
                this.builder.Clear());
        }

        public bool Contains(T item)
        {
            return this.builderLock.DoRead(() =>
                this.builder.Contains(item));
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.builderLock.DoRead(() =>
                Buffer.BlockCopy(this.builder.ToArray(), 0, array, arrayIndex, this.builder.Count));
        }

        public int Count
        {
            get
            {
                return this.builderLock.DoRead(() =>
                    this.builder.Count);
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            return this.builderLock.DoWrite(() =>
                this.builder.Remove(item));
        }

        public int IndexOf(T item)
        {
            return this.builderLock.DoRead(() =>
                this.builder.IndexOf(item));
        }

        public void Insert(int index, T item)
        {
            this.builderLock.DoWrite(() =>
                this.builder.Insert(index, item));
        }

        public void RemoveAt(int index)
        {
            this.builderLock.DoWrite(() =>
                this.builder.RemoveAt(index));
        }

        public T this[int index]
        {
            get
            {
                return this.builderLock.DoRead(() =>
                    this.builder[index]);
            }
            set
            {
                this.builderLock.DoWrite(() =>
                    this.builder[index] = value);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.ToImmutable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public ImmutableList<T> ToImmutable()
        {
            return this.builderLock.DoRead(() =>
                this.builder.ToImmutable());
        }
    }
}
