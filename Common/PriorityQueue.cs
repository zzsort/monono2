using System;
using System.Collections.Generic;
using System.Linq;

namespace monono2.Common
{
    public class PriorityQueue<T>
    {
        private SortedDictionary<int, Queue<T>> m_items = new SortedDictionary<int, Queue<T>>();
        
        public void Push(T value, int priority)
        {
            if (!m_items.TryGetValue(priority, out Queue<T> q))
            {
                q = new Queue<T>();
                m_items.Add(priority, q);
            }
            q.Enqueue(value);
        }

        public T Pop()
        {
            var kvp = m_items.First();
            var result = kvp.Value.Dequeue();
            if (kvp.Value.Count == 0)
                m_items.Remove(kvp.Key);
            return result;
        }

        public bool Empty()
        {
            return m_items.Count == 0;
        }
    }
}
