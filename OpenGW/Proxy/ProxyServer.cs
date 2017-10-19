using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using OpenGW;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public unsafe class ProxyServer
    {
        private readonly Pool<ProxyClientConnection, GWTcpSocket> m_ClientPool;
        
        public GWTcpSocket ServerGwSocket { get; }
        
        public ProxyServerConfiguration Configuration { get; }
        
        

        private void Server_OnAccept(GWTcpSocket gwListener, GWTcpSocket acceptedConnection)
        {
            acceptedConnection.OnReceive += this.Client_OnReceive;
            acceptedConnection.OnCloseConnection += this.Client_OnCloseConnection;
            
            acceptedConnection.UserData = this.m_ClientPool.PopAndInit(acceptedConnection);
            acceptedConnection.StartReceive();
        }

        private void Server_OnAcceptError(GWTcpSocket gwlistener, SocketError error)
        {
            // TODO: Log
        }
        
        
        private void Client_OnReceive(GWTcpSocket gwSocket, byte[] buffer, int offset, int count)
        {
            if (count == 0) {
                gwSocket.Close();
            }

            ProxyClientConnection clientConnection = (ProxyClientConnection)gwSocket.UserData;
            clientConnection.OnReceive(buffer, offset, count);
        }


        private void Client_OnCloseConnection(GWTcpSocket gwSocket)
        {
            ProxyClientConnection clientConnection = (ProxyClientConnection)gwSocket.UserData;
            this.m_ClientPool.Push(clientConnection);
        }

        
        public ProxyServer(ProxyServerConfiguration configuration)
        {
            this.m_ClientPool = new Pool<ProxyClientConnection, GWTcpSocket> {
                Generator = () => new ProxyClientConnection(this),
            };
            
            this.Configuration = configuration;
            
            
            this.ServerGwSocket = GWTcpSocket.CreateServer(
                configuration.BindEndPoint,
                (socket) => {
                    // Set dual mode
                    if (this.Configuration.DualModeIfPossible &&
                        socket.AddressFamily == AddressFamily.InterNetworkV6 &&
                        Socket.OSSupportsIPv6) {
                        socket.DualMode = true;
                    }
                });
            
            this.ServerGwSocket.OnAccept += this.Server_OnAccept;
            this.ServerGwSocket.OnAcceptError += this.Server_OnAcceptError;
        }


        public void Start()
        {
            this.ServerGwSocket.StartAccept();
        }

        public void Stop()
        {
            this.ServerGwSocket.Close();
        }
    }
}
