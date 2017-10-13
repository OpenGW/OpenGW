using System;
using System.Net;
using System.Net.Sockets;

namespace OpenGW.Networking
{
    public delegate void SocketEventOnConnectDelegate(GWSocket connectedSocket);
    public delegate void SocketEventOnConnectErrorDelegate(GWSocket socket, SocketError error);
    public delegate void SocketEventOnReceiveDelegate(GWSocket socket, byte[] buffer, int offset, int count);
    public delegate void SocketEventOnSendDelegate(GWSocket socket, byte[] buffer, int offset, int count);
    public delegate void SocketEventOnCloseConnectionDelegate(GWSocket socket, SocketError error);

    public class GWClient : ISocketEvent
    {
        private readonly IPEndPoint m_RemoteEp;
        public event SocketEventOnConnectDelegate OnConnect;
        public event SocketEventOnConnectErrorDelegate OnConnectError;
        public event SocketEventOnReceiveDelegate OnReceive;
        public event SocketEventOnSendDelegate OnSend;
        public event SocketEventOnCloseConnectionDelegate OnCloseConnection;

        public GWSocket GwSocket { get; }
        
        void ISocketEvent.OnAccept(GWSocket listener, GWSocket acceptSocket)
        {
            throw new ShouldNotReachHereException();
        }

        void ISocketEvent.OnAcceptError(GWSocket listener, SocketError error)
        {
            throw new ShouldNotReachHereException();
        }

        void ISocketEvent.OnConnect(GWSocket connectedSocket)
        {
            this.OnConnect?.Invoke(connectedSocket);
        }

        void ISocketEvent.OnConnectError(GWSocket socket, SocketError error)
        {
            this.OnConnectError?.Invoke(socket, error);
        }

        void ISocketEvent.OnReceive(GWSocket socket, byte[] buffer, int offset, int count)
        {
            this.OnReceive?.Invoke(socket, buffer, offset, count);
        }

        void ISocketEvent.OnSend(GWSocket socket, byte[] buffer, int offset, int count)
        {
            this.OnSend?.Invoke(socket, buffer, offset, count);
        }

        void ISocketEvent.OnCloseConnection(GWSocket socket, SocketError error)
        {
            this.OnCloseConnection?.Invoke(socket, error);
        }

        void ISocketEvent.OnCloseListener(GWSocket listener, SocketError error)
        {
            throw new ShouldNotReachHereException();
        }

        
        public GWClient(Socket socket, IPEndPoint remoteEp)
        {
            this.m_RemoteEp = remoteEp;
            this.GwSocket = new GWSocket(socket, GWSocketType.TcpClientConnection);
        }

        public void AsyncConnect()
        {
            SocketOperation.AsyncConnect(this, this.GwSocket, this.m_RemoteEp);
        }

        public void StartReceive()
        {
            SocketOperation.StartClientReceive(this, this.GwSocket);
        }
        
        public void Close()
        {
            this.GwSocket.Close();
        }

        public void AsyncSend(byte[] buffer, int offset, int count)
        {
            SocketOperation.AsyncSend(this, this.GwSocket, buffer, offset, count);
        }
    }
}
