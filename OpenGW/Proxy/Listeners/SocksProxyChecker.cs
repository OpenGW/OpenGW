using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public class SocksProxyChecker : AbstractProxyChecker
    {
        private const byte SOCKS4_BYTE_VERSION = 0x04;
        private const byte SOCKS5_BYTE_VERSION = 0x05;

        private const byte SOCKS5_BYTE_CMD_CONNECT = 0x01;
        private const byte SOCKS5_BYTE_CMD_BIND = 0x02;
        private const byte SOCKS5_BYTE_CMD_UDP_ASSOCIATE = 0x03;
        
        private const byte SOCKS5_BYTE_ATYPE_IPV4 = 0x01;
        private const byte SOCKS5_BYTE_ATYPE_DOMAINNAME = 0x03;
        private const byte SOCKS5_BYTE_ATYPE_IPV6 = 0x04;
        
        
        
        private static ProxyCheckerResult TrySocksV4(ProxyServer server, GWSocket gwSocket, List<byte> firstBytes)
        {
            Debug.Assert(firstBytes[0] == SOCKS4_BYTE_VERSION);
            
            throw new NotImplementedException();
        }
        
        private static ProxyCheckerResult TrySocksV5(ProxyServer server, GWSocket gwSocket, List<byte> firstBytes)
        {
            Debug.Assert(firstBytes[0] == SOCKS5_BYTE_VERSION);
            
            /*
               +----+----------+----------+
               |VER | NMETHODS | METHODS  |
               +----+----------+----------+
               | 1  |    1     | 1 to 255 |
               +----+----------+----------+
            */
            Debug.Assert(firstBytes[0] == SOCKS5_BYTE_VERSION);
            int nMethods = (int)firstBytes[1];
            if (firstBytes.Count < 2 + nMethods)
            {
                return ProxyCheckerResult.Uncertain;
            }

            // Now firstBytes.Count >= 2 + nMethods
            return ProxyCheckerResult.Success;
            
        }
        
        public static ProxyCheckerResult TryThisProxy(ProxyServer server, GWSocket gwSocket, List<byte> firstBytes)
        {
            if (firstBytes.Count < 2) return ProxyCheckerResult.Uncertain;

            switch (firstBytes[0])
            {
                case SOCKS4_BYTE_VERSION:  // SOCKS 4 / 4a
                    return SocksProxyChecker.TrySocksV4(server, gwSocket, firstBytes);
                    
                case SOCKS5_BYTE_VERSION:  // SOCKS 5
                    return SocksProxyChecker.TrySocksV5(server, gwSocket, firstBytes);
                    
                default:
                    return ProxyCheckerResult.Failed;
            }
        }



        private bool m_Authenticated = false;

        private bool m_AuthenticationSuccessful = false;
        
        

        public ProxyCheckerResult InitializeSocksV4(
            List<byte> firstBytes,
            out AbstractProxyListener proxyListener,
            out int unusedBytes)
        {
            Debug.Assert(firstBytes[0] == SOCKS4_BYTE_VERSION);
            
            throw new NotImplementedException();
        }
        
        public ProxyCheckerResult InitializeSocksV5(
            List<byte> firstBytes,
            out AbstractProxyListener proxyListener,
            out int unusedBytes)
        {
            proxyListener = null;
            unusedBytes = 0;
            
            Debug.Assert(firstBytes[0] == SOCKS5_BYTE_VERSION);

            /*
               The values currently defined for METHOD are:
                  o  X'00' NO AUTHENTICATION REQUIRED
                  o  X'01' GSSAPI
                  o  X'02' USERNAME/PASSWORD
                  o  X'03' to X'7F' IANA ASSIGNED
                  o  X'80' to X'FE' RESERVED FOR PRIVATE METHODS
                  o  X'FF' NO ACCEPTABLE METHODS

             Response:
               The server selects from one of the methods given in METHODS, and
               sends a METHOD selection message:
                         +----+--------+
                         |VER | METHOD |
                         +----+--------+
                         | 1  |   1    |
                         +----+--------+
            */
            
            int nMethods = (int)firstBytes[1];
            
            if (!this.m_Authenticated)
            {
                this.m_Authenticated = true;

                
                const byte AUTH_NO_AUTHENTICATION_REQUIRED = 0x00;
                const byte AUTH_GSSAPI = 0x01;
                const byte AUTH_USERNAME_PASSWORD = 0x02;
                const byte AUTH_NO_ACCEPTABLE_METHODS = 0xFF;

                ArraySegment<byte> authMethods = new ArraySegment<byte>(firstBytes.ToArray(), 2, nMethods);
            
                // TODO: Check configuration now!
                byte[] response = new byte[2];
                response[0] = SOCKS5_BYTE_VERSION;            
                if (!authMethods.Contains(AUTH_NO_AUTHENTICATION_REQUIRED))
                {
                    this.m_AuthenticationSuccessful = false;
                    response[1] = AUTH_NO_ACCEPTABLE_METHODS;
                }
                else
                {
                    this.m_AuthenticationSuccessful = true;
                    response[1] = AUTH_NO_AUTHENTICATION_REQUIRED;
                }
            
                // Send the response
                this.ProxyServer.StartSend(this.ConnectedGwSocket, response, 0, response.Length);
            
                return ProxyCheckerResult.Uncertain;
            }

            // If the authenticaion failed, but the client didn't close the connection
            // Then we will close the connection!
            if (!this.m_AuthenticationSuccessful)
            {
                return ProxyCheckerResult.Failed;
            }

            int ptrOffset = 2 + nMethods;  // the first (2+nMethods) bytes are used, and are of no use now.
            Debug.Assert(firstBytes.Count > ptrOffset);

            if (firstBytes.Count < ptrOffset + 4 /*VER,CMD,RSV,ATYP*/ + 2 /*DST.ADDR*/ + 2 /*DST.PORT*/)
            {
                return ProxyCheckerResult.Uncertain;
            }
            
            /*
                +----+-----+-------+------+----------+----------+
                |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
                +----+-----+-------+------+----------+----------+
                | 1  |  1  | X'00' |  1   | Variable |    2     |
                +----+-----+-------+------+----------+----------+
        
             Where:
        
                  o  VER    protocol version: X'05'
                  o  CMD
                     o  CONNECT X'01'
                     o  BIND X'02'
                     o  UDP ASSOCIATE X'03'
                  o  RSV    RESERVED
                  o  ATYP   address type of following address
                     o  IP V4 address: X'01'
                     o  DOMAINNAME: X'03'
                     o  IP V6 address: X'04'
                  o  DST.ADDR       desired destination address
                  o  DST.PORT desired destination port in network octet
                     order


             In an address field (DST.ADDR, BND.ADDR), the ATYP field specifies
             the type of address contained within the field:
               
              o  X'01'
               the address is a version-4 IP address, with a length of 4 octets
        
              o  X'03'
               the address field contains a fully-qualified domain name.  The first
               octet of the address field contains the number of octets of name that
               follow, there is no terminating NUL octet.
        
              o  X'04'
               the address is a version-6 IP address, with a length of 16 octets.
            */

            byte[] firstBytesArray = firstBytes.ToArray();
            
            byte cmd = firstBytes[ptrOffset + 1];
            if (cmd != SOCKS5_BYTE_CMD_CONNECT &&
                cmd != SOCKS5_BYTE_CMD_BIND &&
                cmd != SOCKS5_BYTE_CMD_UDP_ASSOCIATE)
            {
                return ProxyCheckerResult.Failed;
            }


            byte aType = firstBytes[ptrOffset + 3];
            List<IPAddress> dstAddrs = new List<IPAddress>();
            int dstAddrLength;
            switch (aType)
            {
                case SOCKS5_BYTE_ATYPE_IPV4:
                    dstAddrLength = 4;
                    if (firstBytes.Count < ptrOffset + 4 + dstAddrLength + 2) return ProxyCheckerResult.Uncertain;
                    IList<byte> ipv4 = new ArraySegment<byte>(firstBytesArray, ptrOffset + 4, dstAddrLength);
                    string strIPv4 = $"{ipv4[0]}.{ipv4[1]}.{ipv4[2]}.{ipv4[3]}";
                    dstAddrs.Add(IPAddress.Parse(strIPv4));
                    break;
                case SOCKS5_BYTE_ATYPE_DOMAINNAME:
                    dstAddrLength = 1 + (int)firstBytes[ptrOffset + 4];
                    if (firstBytes.Count < ptrOffset + 4 + dstAddrLength + 2) return ProxyCheckerResult.Uncertain;
                    string hostname = Encoding.ASCII.GetString(
                        new ArraySegment<byte>(firstBytesArray, ptrOffset + 4 + 1, dstAddrLength - 1).ToArray());
                    dstAddrs.AddRange(this.ProxyServer.DnsQuery(hostname));
                    break;
                case SOCKS5_BYTE_ATYPE_IPV6:
                    dstAddrLength = 16;
                    if (firstBytes.Count < ptrOffset + 4 + dstAddrLength + 2) return ProxyCheckerResult.Uncertain;
                    byte[] ipv6 = new ArraySegment<byte>(firstBytesArray, ptrOffset + 4, dstAddrLength).ToArray();
                    string strIPv6 = $"{ipv6[0]:X2}{ipv6[1]:X2}:" +
                                     $"{ipv6[2]:X2}{ipv6[3]:X2}:" +
                                     $"{ipv6[4]:X2}{ipv6[5]:X2}:" +
                                     $"{ipv6[6]:X2}{ipv6[7]:X2}:" +
                                     $"{ipv6[8]:X2}{ipv6[9]:X2}:" +
                                     $"{ipv6[10]:X2}{ipv6[11]:X2}:" +
                                     $"{ipv6[12]:X2}{ipv6[13]:X2}:" +
                                     $"{ipv6[14]:X2}{ipv6[15]:X2}";
                    dstAddrs.Add(IPAddress.Parse(strIPv6));
                    break;
                default:
                    return ProxyCheckerResult.Failed;
            }

            ptrOffset += 4 + dstAddrLength;
            IList<byte> portBytes = new ArraySegment<byte>(firstBytesArray, ptrOffset, 2);
            int port = portBytes[0] << 8 | portBytes[1];

            ptrOffset += 2;
            switch (cmd)
            {
                case SOCKS5_BYTE_CMD_CONNECT:
                    unusedBytes = firstBytes.Count - ptrOffset;
                    proxyListener = new Socks5TcpConnectProxyListener(this.ProxyServer, this.ConnectedGwSocket, dstAddrs, port);
                    return ProxyCheckerResult.Success;

                case SOCKS5_BYTE_CMD_BIND:
                    return ProxyCheckerResult.Failed;  // TODO
                
                case SOCKS5_BYTE_CMD_UDP_ASSOCIATE:
                    return ProxyCheckerResult.Failed;  // TODO
                
                default:
                    // cmd is checked before to garantee valid.
                    throw new NotImplementedException("Should not reach here");
            }
        }
        

        public override ProxyCheckerResult Initialize(
            List<byte> firstBytes, 
            out AbstractProxyListener proxyListener, 
            out int unusedBytes)
        {
            if (firstBytes[0] == SOCKS4_BYTE_VERSION)
            {
                return this.InitializeSocksV4(firstBytes, out proxyListener, out unusedBytes);
            }
            else
            {
                Debug.Assert(firstBytes[0] == SOCKS5_BYTE_VERSION);
                return this.InitializeSocksV5(firstBytes, out proxyListener, out unusedBytes);
            }            
        }
        

        public SocksProxyChecker(ProxyServer proxyServer, GWSocket connectedGwSocket) 
            : base(proxyServer, connectedGwSocket)
        {
        }
    }
}