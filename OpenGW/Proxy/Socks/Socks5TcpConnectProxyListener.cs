using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public class Socks5TcpConnectProxyListener : AbstractProxyListener
    {
        public List<IPAddress> IPAddresses { get; }

        public int Port { get; }

        private Socket m_Socket;
        
        
        public Socks5TcpConnectProxyListener(
            ProxyServer server, GWSocket connectedGwSocket,
            List<IPAddress> ipAddresses, 
            int port)
            : base(server, connectedGwSocket)
        {
            this.IPAddresses = ipAddresses;
            this.Port = port;

            /*
                +----+-----+-------+------+----------+----------+
                |VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
                +----+-----+-------+------+----------+----------+
                | 1  |  1  | X'00' |  1   | Variable |    2     |
                +----+-----+-------+------+----------+----------+
            
                 Where:
            
                      o  VER    protocol version: X'05'
                      o  REP    Reply field:
                         o  X'00' succeeded
                         o  X'01' general SOCKS server failure
                         o  X'02' connection not allowed by ruleset
                         o  X'03' Network unreachable
                         o  X'04' Host unreachable
                         o  X'05' Connection refused
                         o  X'06' TTL expired
                         o  X'07' Command not supported
                         o  X'08' Address type not supported
                         o  X'09' to X'FF' unassigned
                      o  RSV    RESERVED
                      o  ATYP   address type of following address            
                         o  IP V4 address: X'01'
                         o  DOMAINNAME: X'03'
                         o  IP V6 address: X'04'
                      o  BND.ADDR       server bound address
                      o  BND.PORT       server bound port in network octet order
            
               Fields marked RESERVED (RSV) must be set to X'00'.
            
            CONNECT
            
               In the reply to a CONNECT, BND.PORT contains the port number that the
               server assigned to connect to the target host, while BND.ADDR
               contains the associated IP address.  The supplied BND.ADDR is often
               different from the IP address that the client uses to reach the SOCKS
               server, since such servers are often multi-homed.  It is expected
               that the SOCKS server will use DST.ADDR and DST.PORT, and the
               client-side source address and port in evaluating the CONNECT
               request.
                
            */
            try
            {
                IPAddress ip = this.IPAddresses[0];
                this.m_Socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this.m_Socket.Connect(new IPEndPoint(ip, port));

                bool localEndpointIsIPv4 = (this.m_Socket.LocalEndPoint.AddressFamily == AddressFamily.InterNetwork);
                ushort localEndpointPort = (ushort)((IPEndPoint)this.m_Socket.LocalEndPoint).Port;
                byte[] localEndpointPortBytes = new byte[2]
                {  // network order
                    (byte)(localEndpointPort >> 16), 
                    (byte)(localEndpointPort)
                };
                
                List<byte> response = new List<byte>(32);
                response.Add(SocksConst.SOCKS5_VER);  // VER: Socks 5
                response.Add(SocksConst.SOCKS5_CONNECT_REP_SUCCEEDED);  // REP: succeeded
                response.Add(SocksConst.RESERVED);  // REV: RESERVED
                response.Add(localEndpointIsIPv4 
                    ? SocksConst.SOCKS5_BYTE_ATYPE_IPV4 
                    : SocksConst.SOCKS5_BYTE_ATYPE_IPV6);  // ATYP
                response.AddRange(((IPEndPoint)this.m_Socket.LocalEndPoint).Address.GetAddressBytes());  // local endpoint ip
                response.AddRange(localEndpointPortBytes);  // local endpoint port

                this.StartSend(response.ToArray(), 0, response.Count);

                Task.Run(() =>
                {
                    while (true)
                    {
                        try
                        {
                            byte[] buffer = new byte[4096];
                            int recvCnt = this.m_Socket.Receive(buffer);
                            if (recvCnt == 0)
                            {
                                break;
                            }

                            this.StartSend(buffer, 0, recvCnt);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            break;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                
                List<byte> response = new List<byte>(32);
                response.Add(SocksConst.SOCKS5_VER);  // VER: Socks 5
                response.Add(SocksConst.SOCKS5_CONNECT_REP_GENERAL_FAILURE);  // REP: error
                response.Add(SocksConst.RESERVED);  // REV: RESERVED
                response.Add(SocksConst.SOCKS5_BYTE_ATYPE_IPV4);  // ATYP
                response.AddRange(new byte[4]);  // local endpoint ip
                response.AddRange(new byte[2]);  // local endpoint port

                this.StartSend(response.ToArray(), 0, response.Count);
            }
        }

        public override void OnReceiveData(byte[] buffer, int offset, int count)
        {
            int sendCnt = this.m_Socket.Send(buffer, offset, count, SocketFlags.None);
            Debug.Assert(sendCnt == count);
        }

        public override void OnCloseConnection(SocketError status)
        {
            Console.WriteLine($"[SOCKS5-CONNECT] Close: {status}");
            this.m_Socket.Shutdown(SocketShutdown.Both);
        }
    }
}