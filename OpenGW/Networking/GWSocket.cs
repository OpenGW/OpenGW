using System;
using System.Net;
using System.Net.Http.Headers;
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
                    if (!this.Socket.Connected)
                    {
                        return;
                    } 
                    
                    this.Socket.Shutdown(SocketShutdown.Both);
                    //this.Socket.Dispose();  // do not dispose it?
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void UpdateEndPointCache()
        {
            if (this.m_Closed) return;  // TODO: Log an error

            switch (this.Type)
            {
                case GWSocketType.TcpServerListener:
                    this.LocalEndPointCache = (IPEndPoint)this.Socket.LocalEndPoint;
                    break;
                case GWSocketType.TcpServerConnection:
                case GWSocketType.TcpClientConnection:
                    this.LocalEndPointCache = (IPEndPoint)this.Socket.LocalEndPoint;
                    this.RemoteEndPointCache = (IPEndPoint)this.Socket.RemoteEndPoint;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string ToString()
        {
            string localEp = this.LocalEndPointCache?.ToString() ?? "<null>";
            string remoteEp = this.RemoteEndPointCache?.ToString() ?? "<null>";

            switch (this.Type)
            {
                case GWSocketType.TcpServerListener:
                    return $"@{localEp}";
                case GWSocketType.TcpServerConnection:
                    return $"[S<] {localEp} -> {remoteEp}";
                case GWSocketType.TcpClientConnection:
                    return $"[C>] {localEp} -> {remoteEp}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}