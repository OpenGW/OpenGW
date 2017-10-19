using System;
using System.Collections.Concurrent;

namespace OpenGW
{

    public class Pool<TIReuseable> where TIReuseable: IReuseable
    {
        public Func<TIReuseable> Generator { get; set; }
        
        private readonly ConcurrentBag<TIReuseable> m_Pool = new ConcurrentBag<TIReuseable>();

        public Pool(Func<TIReuseable> generator = null)
        {
            this.Generator = generator;
        }

        public TIReuseable PopAndInit()
        {
            if (!this.m_Pool.TryTake(out TIReuseable item))
            {
                item = this.Generator.Invoke();
            }
            
            item.Reinitialize();
            return item;
        }

        public void Push(TIReuseable item)
        {
            item.Recycle();
            
            this.m_Pool.Add(item);
        }
    }
    
    
    public class Pool<TIReuseable, T> where TIReuseable: IReuseable<T>
    {
        public Func<TIReuseable> Generator { get; set; }
        
        private readonly ConcurrentBag<TIReuseable> m_Pool = new ConcurrentBag<TIReuseable>();

        public Pool(Func<TIReuseable> generator = null)
        {
            this.Generator = generator;
        }

        public TIReuseable PopAndInit(T param1)
        {
            if (!this.m_Pool.TryTake(out TIReuseable item))
            {
                item = this.Generator.Invoke();
            }
            
            item.Reinitialize(param1);
            return item;
        }

        public void Push(TIReuseable item)
        {
            item.Recycle();
            
            this.m_Pool.Add(item);
        }
    }
    
    
    public class Pool<TIReuseable, T1, T2> where TIReuseable: IReusable<T1, T2>
    {
        public Func<TIReuseable> Generator { get; set; }
        
        private readonly ConcurrentBag<TIReuseable> m_Pool = new ConcurrentBag<TIReuseable>();

        public Pool(Func<TIReuseable> generator = null)
        {
            this.Generator = generator;
        }

        public TIReuseable PopAndInit(T1 param1, T2 param2)
        {
            if (!this.m_Pool.TryTake(out TIReuseable item))
            {
                item = this.Generator.Invoke();
            }
            
            item.Reinitialize(param1, param2);
            return item;
        }

        public void Push(TIReuseable item)
        {
            item.Recycle();
            
            this.m_Pool.Add(item);
        }
    }
    
}