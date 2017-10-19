using System;
using System.Collections.Generic;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public abstract unsafe class AbstractProxyChecker : IDisposable
    {
        public GWTcpSocket ClientGwSocket { get; }

        public abstract CheckerResult TryCheck(
            byte* recvBuffer, int length, 
            out AbstractProxyChecker newChecker);

        
        public virtual void Dispose()
        {
            // Do nothing
        }
        
        public virtual bool InitializeHandler(
            byte* recvBuffer,
            int length,
            out AbstractProxyHandler handler,
            out int unusedBytes)
        {
            throw new NotImplementedException($"{this.GetType()} doesn't support ${nameof(this.InitializeHandler)}");
        }        
        
        protected AbstractProxyChecker(GWTcpSocket clientGwSocket)
        {
            this.ClientGwSocket = clientGwSocket;
        }
    }
}
