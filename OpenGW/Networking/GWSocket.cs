using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace OpenGW.Networking
{
    public abstract class GWSocket
    {
        protected Socket Socket { get; }

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

    }
}
