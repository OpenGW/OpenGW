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
       
        
        
        private static ProxyCheckerResult TrySocksV4(ProxyServer server, GWSocket gwSocket, List<byte> firstBytes)
        {
            Debug.Assert(firstBytes[0] == SocksConst.SOCKS4_VER);
            
            throw new NotImplementedException();
        }
        
        private static ProxyCheckerResult TrySocksV5(ProxyServer server, GWSocket gwSocket, List<byte> firstBytes)
        {
            Debug.Assert(firstBytes[0] == SocksConst.SOCKS5_VER);
            
            /*
               +----+----------+----------+
               |VER | NMETHODS | METHODS  |
               +----+----------+----------+
               | 1  |    1     | 1 to 255 |
               +----+----------+----------+
            */
            Debug.Assert(firstBytes[0] == SocksConst.SOCKS5_VER);
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
                case SocksConst.SOCKS4_VER:  // SOCKS 4 / 4a
                    return SocksProxyChecker.TrySocksV4(server, gwSocket, firstBytes);
                    
                case SocksConst.SOCKS5_VER:  // SOCKS 5
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
            Debug.Assert(firstBytes[0] == SocksConst.SOCKS4_VER);
            
            throw new NotImplementedException();
        }
        
        public ProxyCheckerResult InitializeSocksV5(
            List<byte> firstBytes,
            out AbstractProxyListener proxyListener,
            out int unusedBytes)
        {
            proxyListener = null;
            unusedBytes = 0;
            
            Debug.Assert(firstBytes[0] == SocksConst.SOCKS5_VER);

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

                
                ArraySegment<byte> authMethods = new ArraySegment<byte>(firstBytes.ToArray(), 2, nMethods);
            
                // TODO: Check configuration now!
                byte[] response = new byte[2];
                response[0] = SocksConst.SOCKS5_VER;            
                if (!authMethods.Contains(SocksConst.SOCKS5_AUTHMETHOD_NO_AUTHENTICATION_REQUIRED))
                {
                    this.m_AuthenticationSuccessful = false;
                    response[1] = SocksConst.SOCKS5_AUTHMETHOD_NO_ACCEPTABLE_METHODS;
                }
                else
                {
                    this.m_AuthenticationSuccessful = true;
                    response[1] = SocksConst.SOCKS5_AUTHMETHOD_NO_AUTHENTICATION_REQUIRED;
                }
            
                // Send the response
                this.ProxyServer.AsyncSend(this.ConnectedGwSocket, response, 0, response.Length);
            
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
            if (cmd != SocksConst.SOCKS5_BYTE_CMD_CONNECT &&
                cmd != SocksConst.SOCKS5_BYTE_CMD_BIND &&
                cmd != SocksConst.SOCKS5_BYTE_CMD_UDP_ASSOCIATE)
            {
                return ProxyCheckerResult.Failed;
            }


            byte aType = firstBytes[ptrOffset + 3];
            List<IPAddress> dstAddrs = new List<IPAddress>();
            int dstAddrLength;
            switch (aType)
            {
                case SocksConst.SOCKS5_BYTE_ATYPE_IPV4:
                case SocksConst.SOCKS5_BYTE_ATYPE_IPV6:
                    dstAddrLength = (aType == SocksConst.SOCKS5_BYTE_ATYPE_IPV4) ? 4 : 16;
                    if (firstBytes.Count < ptrOffset + 4 + dstAddrLength + 2) return ProxyCheckerResult.Uncertain;
                    byte[] ip = new ArraySegment<byte>(firstBytesArray, ptrOffset + 4, dstAddrLength).ToArray();
                    dstAddrs.Add(new IPAddress(ip));
                    break;
                case SocksConst.SOCKS5_BYTE_ATYPE_DOMAINNAME:
                    dstAddrLength = 1 + (int)firstBytes[ptrOffset + 4];
                    if (firstBytes.Count < ptrOffset + 4 + dstAddrLength + 2) return ProxyCheckerResult.Uncertain;
                    string hostname = Encoding.ASCII.GetString(
                        new ArraySegment<byte>(firstBytesArray, ptrOffset + 4 + 1, dstAddrLength - 1).ToArray());
                    dstAddrs.AddRange(this.ProxyServer.DnsQuery(hostname));
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
                case SocksConst.SOCKS5_BYTE_CMD_CONNECT:
                    unusedBytes = firstBytes.Count - ptrOffset;
                    proxyListener = new Socks5TcpConnectProxyListener(this.ProxyServer, this.ConnectedGwSocket, dstAddrs, port);
                    return ProxyCheckerResult.Success;

                case SocksConst.SOCKS5_BYTE_CMD_BIND:
                    return ProxyCheckerResult.Failed;  // TODO
                    throw new NotImplementedException();
                
                case SocksConst.SOCKS5_BYTE_CMD_UDP_ASSOCIATE:
                    return ProxyCheckerResult.Failed;  // TODO
                    throw new NotImplementedException();
                
                default:
                    // cmd is checked before to garantee valid.
                    throw new ShouldNotReachHereException();
            }
        }
        

        public override ProxyCheckerResult Initialize(
            List<byte> firstBytes, 
            out AbstractProxyListener proxyListener, 
            out int unusedBytes)
        {
            if (firstBytes[0] == SocksConst.SOCKS4_VER)
            {
                return this.InitializeSocksV4(firstBytes, out proxyListener, out unusedBytes);
            }
            else
            {
                Debug.Assert(firstBytes[0] == SocksConst.SOCKS5_VER);
                return this.InitializeSocksV5(firstBytes, out proxyListener, out unusedBytes);
            }            
        }
        

        public SocksProxyChecker(ProxyServer proxyServer, GWSocket connectedGwSocket) 
            : base(proxyServer, connectedGwSocket)
        {
        }
    }
}