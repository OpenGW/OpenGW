using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using OpenGW.Networking;

namespace OpenGW.Proxy
{
    public class HttpProxyListener : AbstractProxyListener
    {
        private readonly Socket m_Socket;
    
        public HttpProxyListener(
            ProxyServer server, 
            GWSocket connectedGwSocket,
            string httpVersion,
            string endpoint,
            Dictionary<string, string> headers
            )
            : base(server, connectedGwSocket)
        {
            //Console.WriteLine($"Happily accept a proxy connection: HTTP");
            //Console.WriteLine($"Http    - {httpVersion}");
            //Console.WriteLine($"Domain  - {endpoint}");

            foreach (KeyValuePair<string,string> pair in headers)
            {
                //Console.WriteLine($"Header  - {pair.Key} = {pair.Value}");
            }

            if (!NetUtility.TrySplitHostAndPort(endpoint, out string hostOrIp, out int? optPort))
            {
                Console.WriteLine($"Invalid endpoint: {endpoint}");
                this.Close();  // TODO: Respond "invalid hostname"
            }

            int port = optPort ?? 80;
            try
            {
                IPAddress[] ips = Dns.GetHostAddressesAsync(hostOrIp).Result;
                string strIPs = string.Join(", ", ips.Select(ip => ip.ToString()));
                Console.WriteLine($"[DNS] {hostOrIp} -> {strIPs}");
                
                this.m_Socket = new Socket(ips[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this.m_Socket.Connect(ips[0], port);
                Task.Run(() =>
                {
                    while (true)
                    {
                        byte[] buffer = new byte[4096];
                        int cnt = this.m_Socket.Receive(buffer);
                        if (cnt == 0)
                        {
                            break;
                        }

                        this.AsyncSend(buffer, 0, cnt);
                    }
                });

                Console.WriteLine($"[HttpProxy] Proxy: {hostOrIp}:{port} (HTTP/{httpVersion})");
                
                byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\n\r\n");
                this.AsyncSend(response, 0, response.Length);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                this.Close();
            }
            
        }

        public override void OnReceiveData(byte[] buffer, int offset, int count)
        {
            //Console.WriteLine($"[Receive] Offset = {offset}, Count = {count}");
            int sent = this.m_Socket.Send(buffer, offset, count, SocketFlags.None);
            Debug.Assert(sent == count);
        }

        public override void OnCloseConnection(SocketError status)
        {
            
        }
    }
}