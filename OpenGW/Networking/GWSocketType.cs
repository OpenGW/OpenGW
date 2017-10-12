using System;

namespace OpenGW.Networking
{
    [Flags]
    public enum GWSocketType
    {
        TcpServerListener = 1,
        TcpServerConnection,
        TcpClientConnection,
    }
}