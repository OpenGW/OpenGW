using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace OpenGW.Proxy
{
    public enum ProxyCheckerResult
    {
        Success,
        Failed,
        Uncertain,
    }
    
    public abstract class AbstractProxyChecker
    {
        [ThreadStatic] private static bool[] s_AtLeastOneUncertain;
                
        public static ProxyCheckerResult TryCheck(ProxyServer proxyServer, Socket connectedSocket, List<byte> firstBytes, out AbstractProxyChecker checker)
        {
            if (s_AtLeastOneUncertain == null)
            {
                s_AtLeastOneUncertain = new []{false};
            }
            
            bool TryCheckOne(Func<List<byte>, ProxyCheckerResult> funcTryThisProxy)
            {
                switch (funcTryThisProxy(firstBytes))
                {
                    case ProxyCheckerResult.Success:
                        return true;
                    case ProxyCheckerResult.Failed:
                        return false;
                    case ProxyCheckerResult.Uncertain:
                        s_AtLeastOneUncertain[0] = true;
                        return false;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            s_AtLeastOneUncertain[0] = false;

            if (TryCheckOne(HttpProxyChecker.TryThisProxy))
            {
                checker = new HttpProxyChecker(proxyServer, connectedSocket);
                return ProxyCheckerResult.Success;
            }
            if (TryCheckOne(SocksProxyChecker.TryThisProxy))
            {
                checker = new SocksProxyChecker(proxyServer, connectedSocket);
                return ProxyCheckerResult.Success;
            }

            checker = null;
            return s_AtLeastOneUncertain[0] ? ProxyCheckerResult.Uncertain : ProxyCheckerResult.Failed;
        }

        
        protected readonly ProxyServer ProxyServer;
        protected readonly Socket ConnectedSocket;

        public abstract ProxyCheckerResult Initialize(List<byte> firstBytes, out AbstractProxyListener proxyListener, out int unusedBytes);
                
        protected AbstractProxyChecker(ProxyServer proxyServer, Socket connectedSocket)
        {
            this.ProxyServer = proxyServer;
            this.ConnectedSocket = connectedSocket;
        }
    }
}