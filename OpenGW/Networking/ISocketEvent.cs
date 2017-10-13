using System;
using System.Net.Sockets;

namespace OpenGW.Networking
{
    public interface ISocketEvent
    {   
        void OnAccept(GWSocket listener, GWSocket acceptSocket);
        //event Action<GWSocket, GWSocket> OnAccept;
        
        void OnAcceptError(GWSocket listener, SocketError error);
        
        void OnConnect(GWSocket connectedSocket);
        
        void OnConnectError(GWSocket socket, SocketError error);
        
        void OnReceive(GWSocket socket, byte[] buffer, int offset, int count);
                
        void OnSend(GWSocket socket, byte[] buffer, int offset, int count);
                
        void OnCloseConnection(GWSocket socket, SocketError error);
        
        void OnCloseListener(GWSocket listener, SocketError error);
    }
}
