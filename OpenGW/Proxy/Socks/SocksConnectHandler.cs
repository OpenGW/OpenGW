using OpenGW.Networking;

namespace OpenGW.Proxy.Socks
{
    public class SocksConnectHandler : AbstractProxyHandler
    {
        public SocksConnectHandler(
            GWTcpSocket clientConnectionSocket
            ) : base(clientConnectionSocket)
        {
            
        }

        public override unsafe void OnReceive(byte* recvBuffer, int length)
        {
            
        }
    }
}