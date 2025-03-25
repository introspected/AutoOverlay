using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoOverlay
{
    public class LinkedHashSet<T> : ICollection<T>
    {
        private readonly HashSet<T> set = new();
        private readonly List<T> list = new();

        public IEnumerator<T> GetEnumerator() => list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();

        public void Add(T item)
        {
            if (set.Add(item))
                list.Add(item);
        }

        public void Clear()
        {
            set.Clear();
            list.Clear();
        }

        public bool Contains(T item) => set.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public int Count => list.Count;

        public bool IsReadOnly => false;
    }
}
