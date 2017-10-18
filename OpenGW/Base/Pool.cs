using System;
using System.Collections.Concurrent;

namespace OpenGW
{
    public class Pool<T>
    {        
        public Func<T> Generator { get; set; }        
        public Action<T> Cleaner { get; set; }
        
        private readonly ConcurrentBag<T> m_Pool = new ConcurrentBag<T>();

        public Pool(Func<T> generator = null)
        {
            this.Generator = generator;
        }

        public T Pop()
        {
            if (!this.m_Pool.TryTake(out T item))
            {
                item = this.Generator.Invoke();
            }
            return item;
        }

        public void Push(T item)
        {
            this.Cleaner?.Invoke(item);
            
            this.m_Pool.Add(item);
        }
    }
}
