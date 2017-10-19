using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGW.Networking
{
    public enum GWSocketType
    {
        TcpServerListener,
        TcpServerConnector,
        TcpClientConnector,

        UdpClient,
        // This is no UdpServer
    }
}
