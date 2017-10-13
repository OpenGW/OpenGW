using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public abstract class AbstractProxyListener
    {
        public ProxyServer Server { get; }
        public GWSocket ConnectedGwSocket { get; }

        protected AbstractProxyListener(ProxyServer server, GWSocket connectedGwSocket)
        {
            Debug.Assert(connectedGwSocket.Type == GWSocketType.TcpServerConnection);
            
            this.Server = server;
            this.ConnectedGwSocket = connectedGwSocket;
        }

        public void AsyncSend(byte[] buffer, int offset, int count)
        {
            this.Server.AsyncSend(this.ConnectedGwSocket, buffer, offset, count);
        }

        public void Close()
        {
            this.ConnectedGwSocket.Close();
        }
        
        public abstract void OnReceiveData(byte[] buffer, int offset, int count);

        public abstract void OnCloseConnection(SocketError status);
    }
}