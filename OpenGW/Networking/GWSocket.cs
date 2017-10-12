using System;
using System.Net;
using System.Net.Sockets;

namespace OpenGW.Networking
{
    /// <summary>
    /// To store some external information for a socket
    /// Mainly convenient for debugging
    /// </summary>
    public class GWSocket
    {
        public Socket Socket { get; }

        public GWSocketType Type { get; set; }

        public IPEndPoint RemoteEndPointCache { get; set; }
        
        public IPEndPoint LocalEndPointCache { get; set; }
        
        public string HostOrIP { get; set; }

        
        private bool m_Closed = false;
        
        public GWSocket(Socket socket, GWSocketType type)
        {
            this.Socket = socket;
            this.Type = type;
        }

        public void Close()
        {
            if (this.m_Closed) return;
            this.m_Closed = true;

            switch (this.Type)
            {
                case GWSocketType.TcpServerListener:
                    this.Socket.Dispose();
                    break;
                case GWSocketType.TcpServerConnection:
                case GWSocketType.TcpClientConnection:
                    this.Socket.Shutdown(SocketShutdown.Both);
                    this.Socket.Dispose();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}