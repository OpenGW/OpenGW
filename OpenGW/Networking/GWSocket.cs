using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OpenGW.Networking
{
    public abstract class GWSocket
    {
        protected Socket Socket { get; }

        public IPEndPoint LocalEndPoint { get; protected set; }

        public IPEndPoint RemoteEndPoint { get; protected set; }


        protected GWSocketType Type { get; }

        protected Action<Socket> SocketPropertySetter { get; }

        protected GWSocket(Socket socket, GWSocketType type, Action<Socket> socketPropertySetter)
        {
            this.Socket = socket;
            this.Type = type;
            this.SocketPropertySetter = socketPropertySetter;

            switch (this.Type) {
                case GWSocketType.TcpServerListener:
                case GWSocketType.TcpServerConnector:
                case GWSocketType.TcpClientConnector:
                    Debug.Assert(socket.ProtocolType == ProtocolType.Tcp);
                    Debug.Assert(socket.SocketType == SocketType.Stream);
                    break;
                case GWSocketType.UdpClient:
                    Debug.Assert(socket.ProtocolType == ProtocolType.Udp);
                    Debug.Assert(socket.SocketType == SocketType.Dgram);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            this.SocketPropertySetter?.Invoke(socket);
        }

        public override string ToString()
        {
            switch (this.Type) {
                case GWSocketType.TcpServerListener:
                    return $"(S@ {this.LocalEndPoint})";
                case GWSocketType.TcpServerConnector:
                    return $"(S> {this.LocalEndPoint}->{this.RemoteEndPoint})";
                case GWSocketType.TcpClientConnector:
                    return $"(S> {this.LocalEndPoint?.ToString() ?? "<null>"}->{this.RemoteEndPoint?.ToString() ?? "<null>"})";
                case GWSocketType.UdpClient:
                    return $"(U@ {this.LocalEndPoint})";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
