using System.Collections.Generic;
using System.Net.Sockets;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public class SocksProxyListener : AbstractProxyListener
    {
        public static bool? TryProxyHeader(List<byte> firstBytes)
        {
            if (firstBytes.Count < 2)
            {
                return null;
            }
            
            if (firstBytes[0] == 0x04)
            {
                return true;
            }
            else if (firstBytes[0] == 0x05)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
                
        public SocksProxyListener(ProxyServer server, GWSocket connectedGwSocket) : base(server, connectedGwSocket)
        {
        }

        public override void OnReceiveData(byte[] buffer, int offset, int count)
        {
            
        }

        public override void OnCloseConnection(SocketError status)
        {
            
        }
    }
}