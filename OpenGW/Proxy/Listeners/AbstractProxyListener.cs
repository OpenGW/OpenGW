using System.Collections.Generic;
using System.Net.Sockets;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public abstract class AbstractProxyListener
    {
        public ProxyServer Server { get; }
        public Socket ConnectedSocket { get; }

        protected AbstractProxyListener(ProxyServer server, Socket connectedSocket)
        {
            this.Server = server;
            this.ConnectedSocket = connectedSocket;
        }

        public void StartSend(byte[] buffer, int offset, int count)
        {
            SocketOperation.StartSend(this.Server, this.ConnectedSocket, buffer, offset, count);
        }
        
        public abstract void OnReceiveData(byte[] buffer, int offset, int count);

        public abstract void OnCloseConnection(SocketError status);
    }
}