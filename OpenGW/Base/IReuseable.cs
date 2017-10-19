namespace OpenGW
{
    public interface IReuseable
    {
        void Reinitialize();
        
        void Recycle();
    }
    
    public interface IReuseable<in T>
    {
        void Reinitialize(T param);
        
        void Recycle();
    }
    
    public interface IReusable<in T1, in T2>
    {
        void Reinitialize(T1 param1, T2 param2);
        
        void Recycle();
    }
    
    public interface IReusable<in T1, in T2, in T3>
    {
        void Reinitialize(T1 param1, T2 param2, T3 param3);
        
        void Recycle();
    }
    
}
