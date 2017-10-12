using System;
using System.Net.Sockets;

namespace OpenGW.Networking
{
    public interface ISocketEvent
    {        
        void OnAccept(Socket listener, Socket acceptSocket);

        void OnAcceptError(Socket listener, SocketError error);
        
        void OnConnect(Socket connectedSocket);
        
        void OnConnectError(Socket socket, SocketError error);
        
        void OnReceive(Socket socket, byte[] buffer, int offset, int count);
        
        void OnReceiveError(Socket socket, SocketError error);
        
        void OnSend(Socket socket, byte[] buffer, int offset, int count);
        
        void OnSendError(Socket socket, SocketError error);
        
        void OnCloseConnection(Socket socket, SocketError error);
        
        void OnCloseListener(Socket listener, SocketError error);
    }
}
