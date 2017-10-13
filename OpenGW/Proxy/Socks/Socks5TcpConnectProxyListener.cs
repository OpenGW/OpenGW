using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public class Socks5TcpConnectProxyListener : AbstractProxyListener
    {
        public List<IPAddress> IPAddresses { get; }

        public int Port { get; }

        private GWClient m_Client;
        
        
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
            IPAddress ip = this.IPAddresses[0];
            Socket clientSocket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            ManualResetEventSlim mreConnect = new ManualResetEventSlim(false);

            Wrapper<bool> success = (Wrapper<bool>)false;
            this.m_Client = new GWClient(clientSocket, new IPEndPoint(ip, port));
            this.m_Client.OnCloseConnection += (gwSocket, error) =>
            {
                this.Close();
            };
            this.m_Client.OnConnect += gwSocket =>
            {
                success.Value = true;
                mreConnect.Set();
            };
            this.m_Client.OnConnectError += (gwSocket, error) =>
            {
                Logger.Error($"[SOCKS] Server failed to connect {new IPEndPoint(ip, port)}: {error}");
                
                List<byte> badResponse = new List<byte>(32);
                badResponse.Add(SocksConst.SOCKS5_VER);  // VER: Socks 5
                badResponse.Add(SocksConst.SOCKS5_CONNECT_REP_GENERAL_FAILURE);  // REP: error
                badResponse.Add(SocksConst.RESERVED);  // REV: RESERVED
                badResponse.Add(SocksConst.SOCKS5_BYTE_ATYPE_IPV4);  // ATYP
                badResponse.AddRange(new byte[4]);  // local endpoint ip
                badResponse.AddRange(new byte[2]);  // local endpoint port

                this.AsyncSend(badResponse.ToArray(), 0, badResponse.Count);

                success.Value = false;
                mreConnect.Set();
            };
            this.m_Client.AsyncConnect();
            mreConnect.Wait();

            // SOCKS server is not successfully connected
            if (!success.Value)
            {
                this.Close();
                return;
            }
            
            
            //IPEndPoint localEp = this.m_Client.GwSocket.LocalEndPointCache; // TODO: Can be null sometimes - why?
            IPEndPoint localEp = (IPEndPoint)this.m_Client.GwSocket.Socket.LocalEndPoint;
            Debug.Assert(localEp != null);
            bool localEndpointIsIPv4 = (localEp.AddressFamily == AddressFamily.InterNetwork);
            ushort localEndpointPort = (ushort)localEp.Port;
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
            response.AddRange(localEp.Address.GetAddressBytes());  // local endpoint ip
            response.AddRange(localEndpointPortBytes);  // local endpoint port

            this.AsyncSend(response.ToArray(), 0, response.Count);
            
            this.m_Client.OnReceive += (gwSocket, buffer, offset, count) =>
            {
                this.AsyncSend(buffer, offset, count);
            };
            this.m_Client.StartReceive();
        }

        public override void OnReceiveData(byte[] buffer, int offset, int count)
        {
            this.m_Client.AsyncSend(buffer, offset, count);
        }

        public override void OnCloseConnection(SocketError status)
        {
            if (status != SocketError.Success)
            {
                Logger.Error($"SOCKS 5 proxy: {status}");
            }
            this.m_Client.Close();  // OK even if m_Client is not connected!
        }
    }
}