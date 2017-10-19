using System;
using System.Collections.Concurrent;

namespace OpenGW
{
    public class Pool<TReusable> where TReusable: IReusable
    {
        private readonly ConcurrentBag<TReusable> m_Pool = new ConcurrentBag<TReusable>();


        public Func<TReusable> Generator { get; set; }

        public Pool(Func<TReusable> generator = null)
        {
            this.Generator = generator;
        }

        public TReusable Pop()
        {
            if (!this.m_Pool.TryTake(out TReusable item)) {
                item = this.Generator.Invoke();
            }

            item.Reinitialize();
            return item;
        }

        public void Push(TReusable item)
        {
            item.Recycle();

            this.m_Pool.Add(item);
        }
    }


    public class Pool<TReusable, T> where TReusable: IReusable<T>
    {
        private readonly ConcurrentBag<TReusable> m_Pool = new ConcurrentBag<TReusable>();


        public Func<TReusable> Generator { get; set; }

        public Pool(Func<TReusable> generator = null)
        {
            this.Generator = generator;
        }

        public TReusable Pop(T reinitParam)
        {
            if (!this.m_Pool.TryTake(out TReusable item)) {
                item = this.Generator.Invoke();
            }

            item.Reinitialize(reinitParam);
            return item;
        }

        public void Push(TReusable item)
        {
            item.Recycle();

            this.m_Pool.Add(item);
        }
    }
}
