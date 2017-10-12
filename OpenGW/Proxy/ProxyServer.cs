using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public class ProxyServer : ISocketEvent
    {
        private class ProxyListenerInformation
        {
            public readonly List<byte> ReceiveBuffer = new List<byte>();
            public AbstractProxyListener ProxyListener = null;
        }
        
        public ProxyConfiguration Configuration { get; }

        
        private readonly ConcurrentDictionary<Socket, ProxyListenerInformation> m_ActiveProxyListeners
            = new ConcurrentDictionary<Socket, ProxyListenerInformation>();
        
        private Socket m_ServerSocket;
        
        
        public ProxyServer(ProxyConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public void Start()
        {
            this.m_ServerSocket = new Socket(
                this.Configuration.ListenEndpoint.AddressFamily,
                SocketType.Stream, 
                ProtocolType.Tcp);
            
            if (Socket.OSSupportsIPv6 && 
                this.Configuration.ListenEndpoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                this.m_ServerSocket.DualMode = this.Configuration.DualModeIfPossible;
            }
            this.m_ServerSocket.LingerState = new LingerOption(true, 0);  // don't linger
            
            this.m_ServerSocket.Bind(this.Configuration.ListenEndpoint);
            
            this.m_ServerSocket.Listen(0);
            
            SocketOperation.StartAccept(this, this.m_ServerSocket);
        }

        public void Stop()
        {
            this.m_ServerSocket?.Dispose();
        }
        
        
        void ISocketEvent.OnAccept(Socket listener, Socket acceptSocket)
        {
            // TODO: Log
            Console.WriteLine($"[Accept]");

            bool success = this.m_ActiveProxyListeners.TryAdd(acceptSocket, new ProxyListenerInformation());
            Debug.Assert(success);
        }

        void ISocketEvent.OnAcceptError(Socket listener, SocketError error)
        {
            // TODO: Log
            Console.WriteLine($"[AcceptError] {error}");
        }

        void ISocketEvent.OnConnect(Socket connectedSocket)
        {
            throw new NotImplementedException("Should not reach here");
        }

        void ISocketEvent.OnConnectError(Socket socket, SocketError error)
        {
            throw new NotImplementedException("Should not reach here");
        }

        void ISocketEvent.OnReceive(Socket socket, byte[] buffer, int offset, int count)
        {
            if (!this.m_ActiveProxyListeners.TryGetValue(socket, out ProxyListenerInformation info))
            {
                // This socket has been closed
                return;
            }

            if (info.ProxyListener != null)
            {
                Debug.Assert(info.ProxyListener.Server == this);
                Debug.Assert(info.ProxyListener.ConnectedSocket == socket);
                info.ProxyListener.OnReceiveData(buffer, offset, count);
                return;
            }
            
            // Now there is no proxy listener
            // Try to distinguish one from the received bytes
            info.ReceiveBuffer.AddRange(new ArraySegment<byte>(buffer, offset, count));

            bool atLeastOneUncertainProxyType = false;
            do
            {
                // TODO: Configure: whether this listener supports HTTP proxy
                bool? result = HttpProxyListener.TryProxyHeader(info.ReceiveBuffer);
                atLeastOneUncertainProxyType = atLeastOneUncertainProxyType || !result.HasValue;
                if (result.HasValue && result.Value)
                {
                    info.ProxyListener = new HttpProxyListener(this, socket);
                    break;
                }
                
                // TODO: Configure: whether this listener supports SOCKS proxy
                result = SocksProxyListener.TryProxyHeader(info.ReceiveBuffer);
                atLeastOneUncertainProxyType = atLeastOneUncertainProxyType || !result.HasValue;
                if (result.HasValue && result.Value)
                {
                    info.ProxyListener = new SocksProxyListener(this, socket);
                    break;
                }
            } while (false);

            if (info.ProxyListener != null)
            {
                byte[] receivedBytes = info.ReceiveBuffer.ToArray();
                info.ProxyListener.OnReceiveData(receivedBytes, 0, receivedBytes.Length);
            }
            else if (!atLeastOneUncertainProxyType)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Dispose();
            }
        }

        void ISocketEvent.OnReceiveError(Socket socket, SocketError error)
        {
            // TODO: Log
            Console.WriteLine($"[ReceiveError] {error}");
        }

        void ISocketEvent.OnSend(Socket socket, byte[] buffer, int offset, int count)
        {
            // TODO: Log
            Console.WriteLine($"[Send]");
        }

        void ISocketEvent.OnSendError(Socket socket, SocketError error)
        {
            // TODO: Log
            Console.WriteLine($"[SendError] {error}");
        }

        void ISocketEvent.OnCloseConnection(Socket socket, SocketError error)
        {
            // TODO: Log
            Console.WriteLine($"[CloseConnection] {error}");
            
            bool success = this.m_ActiveProxyListeners.TryRemove(socket, out ProxyListenerInformation dummy);
            Debug.Assert(success);
            
            dummy.ProxyListener?.OnCloseConnection(error);
        }

        void ISocketEvent.OnCloseListener(Socket listener, SocketError error)
        {
            // TODO: Log
            Console.WriteLine($"[CloseListener] {error}");
        }
    }
}
