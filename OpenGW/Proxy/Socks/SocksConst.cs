
namespace OpenGW.Proxy
{
    /// <summary>
    /// All constants about SOCKS proxy is put here
    /// </summary>
    public static partial class SocksConst
    {
        // For SOCKS 5

        public const byte SOCKS5_VER = 0x05;
        
        public const byte SOCKS5_BYTE_CMD_CONNECT = 0x01;
        public const byte SOCKS5_BYTE_CMD_BIND = 0x02;
        public const byte SOCKS5_BYTE_CMD_UDP_ASSOCIATE = 0x03;
        
        public const byte SOCKS5_BYTE_ATYPE_IPV4 = 0x01;
        public const byte SOCKS5_BYTE_ATYPE_DOMAINNAME = 0x03;
        public const byte SOCKS5_BYTE_ATYPE_IPV6 = 0x04;

        public const byte SOCKS5_AUTHMETHOD_NO_AUTHENTICATION_REQUIRED = 0x00;
        public const byte SOCKS5_AUTHMETHOD_GSSAPI = 0x01;
        public const byte SOCKS5_AUTHMETHOD_USERNAME_PASSWORD = 0x02;
        public const byte SOCKS5_AUTHMETHOD_NO_ACCEPTABLE_METHODS = 0xFF;

        public const byte SOCKS5_CONNECT_REP_SUCCEEDED = 0x00;
        public const byte SOCKS5_CONNECT_REP_GENERAL_FAILURE = 0x01;
        public const byte SOCKS5_CONNECT_REP_NOT_ALLOWED = 0x02;
        public const byte SOCKS5_CONNECT_REP_NETWORK_UNREACHABLE = 0x03;
        public const byte SOCKS5_CONNECT_REP_HOST_UNREACHABLE = 0x04;
        public const byte SOCKS5_CONNECT_REP_CONNECTION_REFUSED = 0x05;
        public const byte SOCKS5_CONNECT_REP_TTL_EXPIRED = 0x06;
        public const byte SOCKS5_CONNECT_REP_COMMAND_NOT_SUPPORTED = 0x07;
        public const byte SOCKS5_CONNECT_REP_ADDRESS_TYPE_NOT_SUPPORTED = 0x08;

    }
    
    public static partial class SocksConst
    {
        // For SOCKS 4, 4a

        public const byte SOCKS4_VER = 0x04;
    }
    
    public static partial class SocksConst
    {
        // For SOCKS common

        public const byte RESERVED = 0x00;
    }
}
