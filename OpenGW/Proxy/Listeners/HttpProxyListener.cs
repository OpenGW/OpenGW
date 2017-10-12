using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace OpenGW.Proxy
{
    public class HttpProxyListener : AbstractProxyListener
    {
    
        public HttpProxyListener(
            ProxyServer server, 
            Socket connectedSocket,
            string httpVersion,
            string hostname,
            Dictionary<string, string> headers
            )
            : base(server, connectedSocket)
        {
            Console.WriteLine($"Happily accept a proxy connection: HTTP");
            Console.WriteLine($"Http    - {httpVersion}");
            Console.WriteLine($"Domain  - {hostname}");

            foreach (KeyValuePair<string,string> pair in headers)
            {
                Console.WriteLine($"Header  - {pair.Key} = {pair.Value}");
            }

            
            byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\n\r\n");
            this.StartSend(response, 0, response.Length);
        }

        public override void OnReceiveData(byte[] buffer, int offset, int count)
        {
            Console.WriteLine($"[Receive] Offset = {offset}, Count = {count}");
        }

        public override void OnCloseConnection(SocketError status)
        {
            
        }
    }
}