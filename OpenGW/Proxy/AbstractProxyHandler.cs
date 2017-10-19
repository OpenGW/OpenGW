using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public abstract unsafe class AbstractProxyHandler
    {
        public GWTcpSocket ClientConnectionSocket { get; }
        
        
        public abstract void OnReceive(byte* recvBuffer, int length);

        protected AbstractProxyHandler(GWTcpSocket clientConnectionSocket)
        {
            this.ClientConnectionSocket = clientConnectionSocket;
        }
    }
}
