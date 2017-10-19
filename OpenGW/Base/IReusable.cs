using System;

namespace OpenGW
{
    public interface IReusable
    {
        void Reinitialize();

        void Recycle();
    }

    public interface IReusable<in T>
    {
        void Reinitialize(T param);

        void Recycle();
    }
}
