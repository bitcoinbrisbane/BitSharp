using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Common
{
    public static class ImmutableArray
    {
        public static ImmutableArray<T> Create<T>()
        {
            return new ImmutableArray<T>(new T[0], clone: false);
        }

        public static ImmutableArray<T> Create<T>(params T[] items)
        {
            return new ImmutableArray<T>(items, clone: true);
        }

        public static ImmutableArray<T> Create<T>(T item)
        {
            return new ImmutableArray<T>(item);
        }

        public static ImmutableArray<T> CreateRange<T>(IEnumerable<T> items)
        {
            return new ImmutableArray<T>(items.ToArray(), clone: false);
        }

        public static ImmutableArray<T> ToImmutableArray<T>(this T[] source)
        {
            return new ImmutableArray<T>(source, clone: true);
        }

        public static ImmutableArray<T> ToImmutableArray<T>(this IEnumerable<T> source)
        {
            return new ImmutableArray<T>(source.ToArray(), clone: false);
        }
    }

    public struct ImmutableArray<T> : IList<T>, IList
    //, IEquatable<ImmutableArray<T>>, IStructuralComparable, IStructuralEquatable
    {
        private readonly T[] array;

        internal ImmutableArray(T item)
        {
            this.array = new T[1];
            this.array[0] = item;
        }

        internal ImmutableArray(T[] array, bool clone)
        {
            if (array == null)
                throw new ArgumentNullException();

            if (clone)
                this.array = (T[])array.Clone();
            else
                this.array = array;
        }

        public ImmutableArray<T> Add(T value)
        {
            var newArray = new T[this.array.Length + 1];
            this.array.CopyTo(newArray, 0);
            newArray[newArray.Length - 1] = value;

            return new ImmutableArray<T>(newArray, clone: false);
        }

        public ImmutableArray<T> AddRange(IEnumerable<T> items)
        {
            var itemsArray = items.ToArray();

            var newArray = new T[this.array.Length + itemsArray.Length];
            this.array.CopyTo(newArray, 0);
            itemsArray.CopyTo(newArray, this.array.Length);

            return new ImmutableArray<T>(newArray, clone: false);
        }

        public ImmutableArray<T> Clear()
        {
            return new ImmutableArray<T>(new T[0], clone: false);
        }

        public int IndexOf(T item, int index, int count, IEqualityComparer<T> equalityComparer)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> Insert(int index, T element)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> InsertRange(int index, IEnumerable<T> items)
        {
            throw new NotImplementedException();
        }

        public int LastIndexOf(T item, int index, int count, IEqualityComparer<T> equalityComparer)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> Remove(T value, IEqualityComparer<T> equalityComparer)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> RemoveAll(Predicate<T> match)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> RemoveRange(int index, int count)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> RemoveRange(IEnumerable<T> items, IEqualityComparer<T> equalityComparer)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> Replace(T oldValue, T newValue, IEqualityComparer<T> equalityComparer)
        {
            throw new NotImplementedException();
        }

        public ImmutableArray<T> SetItem(int index, T value)
        {
            throw new NotImplementedException();
        }

        public T this[int index]
        {
            get { return this.array[index]; }
        }

        public int Count
        {
            get { return this.array.Length; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)this.array).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.array.GetEnumerator();
        }

        public T[] ToArray()
        {
            var arrayCopy = new T[this.Count];
            this.array.CopyTo(arrayCopy, 0);
            return arrayCopy;
        }

        #region IList<T>

        int IList<T>.IndexOf(T item)
        {
            return Array.IndexOf(this.array, item);
        }

        void IList<T>.Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        T IList<T>.this[int index]
        {
            get
            {
                return this.array[index];
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        void ICollection<T>.Add(T item)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }

        bool ICollection<T>.Contains(T item)
        {
            return Array.IndexOf(this.array, item) >= 0;
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            this.array.CopyTo(array, arrayIndex);
        }

        int ICollection<T>.Count
        {
            get { return this.array.Length; }
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return true; }
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return ((IEnumerable<T>)this.array).GetEnumerator();
        }

        #endregion

        #region IList

        int IList.Add(object value)
        {
            throw new NotSupportedException();
        }

        void IList.Clear()
        {
            throw new NotSupportedException();
        }

        bool IList.Contains(object value)
        {
            return Array.IndexOf(this.array, value) >= 0;
        }

        int IList.IndexOf(object value)
        {
            return Array.IndexOf(this.array, value);
        }

        void IList.Insert(int index, object value)
        {
            throw new NotSupportedException();
        }

        bool IList.IsFixedSize
        {
            get { return true; }
        }

        bool IList.IsReadOnly
        {
            get { return true; }
        }

        void IList.Remove(object value)
        {
            throw new NotSupportedException();
        }

        void IList.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        object IList.this[int index]
        {
            get
            {
                return this.array[index];
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            this.array.CopyTo(array, index);
        }

        int ICollection.Count
        {
            get { return this.array.Length; }
        }

        bool ICollection.IsSynchronized
        {
            get { return true; }
        }

        object ICollection.SyncRoot
        {
            get { return this.array.SyncRoot; }
        }

        #endregion
    }
}
