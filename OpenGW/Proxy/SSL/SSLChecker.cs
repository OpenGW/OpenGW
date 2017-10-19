using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public unsafe class SSLChecker : AbstractProxyChecker
    {
        public SSLChecker(GWTcpSocket clientGwSocket) 
            : base(clientGwSocket)
        {
        }

        public override CheckerResult TryCheck(
            byte* recvBuffer, 
            int length, 
            out AbstractProxyChecker newChecker)
        {
            throw new System.NotImplementedException();
        }

    }
}