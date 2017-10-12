using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
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
            public AbstractProxyChecker ProxyChecker = null;
            public AbstractProxyListener ProxyListener = null;
        }
        
        public ProxyConfiguration Configuration { get; }

        
        private readonly ConcurrentDictionary<GWSocket, ProxyListenerInformation> m_ActiveProxyListeners
            = new ConcurrentDictionary<GWSocket, ProxyListenerInformation>();
        
        private GWSocket m_ServerGwSocket;
        
        
        public ProxyServer(ProxyConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public void Start()
        {
            Socket serverSocket = new Socket(
                this.Configuration.ListenEndpoint.AddressFamily,
                SocketType.Stream, 
                ProtocolType.Tcp);
                
            if (Socket.OSSupportsIPv6 && 
                this.Configuration.ListenEndpoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                serverSocket.DualMode = this.Configuration.DualModeIfPossible;
            }
            serverSocket.LingerState = new LingerOption(true, 0);  // don't linger
            
            serverSocket.Bind(this.Configuration.ListenEndpoint);
            
            serverSocket.Listen(0);
            
            this.m_ServerGwSocket = new GWSocket(serverSocket, GWSocketType.TcpServerListener);
            SocketOperation.StartAccept(this, this.m_ServerGwSocket);
        }

        public void Stop()
        {
            this.m_ServerGwSocket.Close();
        }

        public void StartSend(GWSocket connectedSocket, byte[] buffer, int offset, int count)
        {
            SocketOperation.StartSend(this, connectedSocket, buffer, offset, count);
        }
        
        void ISocketEvent.OnAccept(GWSocket listener, GWSocket acceptGwSocket)
        {
            Debug.Assert(listener.Type == GWSocketType.TcpServerListener);

            // TODO: Log
            // Console.WriteLine($"[Accept]");

            bool success = this.m_ActiveProxyListeners.TryAdd(acceptGwSocket, new ProxyListenerInformation());
            Debug.Assert(success);
        }

        void ISocketEvent.OnAcceptError(GWSocket gwListener, SocketError error)
        {
            Debug.Assert(gwListener.Type == GWSocketType.TcpServerListener);
            
            // TODO: Log
            Console.WriteLine($"[AcceptError] ({gwListener}) {error}");
        }

        void ISocketEvent.OnConnect(GWSocket connectedSocket)
        {
            throw new ShouldNotReachHereException();
        }

        void ISocketEvent.OnConnectError(GWSocket socket, SocketError error)
        {
            throw new ShouldNotReachHereException();
        }

        void ISocketEvent.OnReceive(GWSocket gwSocket, byte[] buffer, int offset, int count)
        {
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnection);
            
            if (!this.m_ActiveProxyListeners.TryGetValue(gwSocket, out ProxyListenerInformation info))
            {
                // This socket has been closed
                return;
            }

            if (info.ProxyListener != null)
            {
                Debug.Assert(info.ProxyListener.Server == this);
                Debug.Assert(info.ProxyListener.ConnectedGwSocket == gwSocket);

                info.ProxyListener.OnReceiveData(buffer, offset, count);
                return;
            }

            // Now there is no proxy listener
            // Try to distinguish one from the received bytes
            info.ReceiveBuffer.AddRange(new ArraySegment<byte>(buffer, offset, count));

            // If there is no even the proxy checker, try to get a (general) checker
            if (info.ProxyChecker == null)
            {
                // AbstractProxyChecker.TryCheck might return null.
                switch (AbstractProxyChecker.TryCheck(this, gwSocket, info.ReceiveBuffer, out info.ProxyChecker))
                {
                    case ProxyCheckerResult.Success:
                        Debug.Assert(info.ProxyChecker != null);
                        break;
                    case ProxyCheckerResult.Failed:
                        Debug.Assert(info.ProxyChecker == null);
                        gwSocket.Close();
                        return;
                    case ProxyCheckerResult.Uncertain:
                        Debug.Assert(info.ProxyChecker == null);
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            Debug.Assert(info.ProxyChecker != null);
            Debug.Assert(info.ProxyListener == null);

            // info.ProxyChecker.Initialize may return null
            switch (info.ProxyChecker.Initialize(info.ReceiveBuffer, out info.ProxyListener, out int unusedBytes))
            {
                case ProxyCheckerResult.Success:
                    Debug.Assert(info.ProxyListener != null);
                    if (unusedBytes > 0)
                    {
                        byte[] receivedBytes = info.ReceiveBuffer.ToArray();
                        info.ProxyListener.OnReceiveData(
                            receivedBytes,
                            receivedBytes.Length - unusedBytes,
                            unusedBytes);
                    }
                    break;
                case ProxyCheckerResult.Failed:
                    Debug.Assert(info.ProxyListener == null);
                    gwSocket.Close();
                    break;
                case ProxyCheckerResult.Uncertain:
                    Debug.Assert(info.ProxyListener == null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        void ISocketEvent.OnSend(GWSocket gwSocket, byte[] buffer, int offset, int count)
        {
            // TODO: Log
            //Console.WriteLine($"[Send]");
        }

        void ISocketEvent.OnCloseConnection(GWSocket gwSocket, SocketError error)
        {
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnection);
            
            // TODO: Log
            Console.WriteLine($"[CloseConnection] ({gwSocket}) {error}");
            
            bool success = this.m_ActiveProxyListeners.TryRemove(gwSocket, out ProxyListenerInformation info);
            Debug.Assert(success);
            
            info.ProxyListener?.OnCloseConnection(error);
        }

        void ISocketEvent.OnCloseListener(GWSocket listener, SocketError error)
        {
            Debug.Assert(listener.Type == GWSocketType.TcpServerListener);
            
            // TODO: Log
            Console.WriteLine($"[CloseListener] ({listener}) {error}");
        }
        
        public IPAddress[] DnsQuery(string hostOrIp)
        {
            // TODO
            Console.WriteLine($"Trying to resolve: {hostOrIp}");
            return Dns.GetHostAddressesAsync(hostOrIp).Result;
        }

    }
}
