using System.Collections.Generic;
using System.Net.Sockets;

namespace OpenGW.Proxy
{
    public class SocksProxyChecker : AbstractProxyChecker
    {
        public static ProxyCheckerResult TryThisProxy(List<byte> firstBytes)
        {
            return ProxyCheckerResult.Failed;
            // TODO: NotImplemented;
        }
        
        public override ProxyCheckerResult Initialize(List<byte> firstBytes, out AbstractProxyListener proxyListener, out int unusedBytes)
        {
            throw new System.NotImplementedException();
        }

        public SocksProxyChecker(ProxyServer proxyServer, Socket connectedSocket) 
            : base(proxyServer, connectedSocket)
        {
        }
    }
}