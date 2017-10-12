using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenGW.Proxy
{
    public class HttpProxyListener : AbstractProxyListener
    {
        private static readonly Regex s_RegexHttpHeaders 
            = new Regex(@"^CONNECT(\s+)(?<HOSTNAME>\S+)(\s+)HTTP/(?<HTTP_VERSION>[0-9\.]+)(\s*\r?\n)((?<HEADERS>[^\n]+?)\r?\n)*(\r?\n)$",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
        
        public static bool? TryProxyHeader(List<byte> firstBytes)
        {
            if (firstBytes.Count < "CONNECT".Length)
            {
                return null;
            }

            if (!((firstBytes[0] == 'C' || firstBytes[0] == 'c') &&
                  (firstBytes[1] == 'O' || firstBytes[1] == 'o') &&
                  (firstBytes[2] == 'N' || firstBytes[2] == 'n') &&
                  (firstBytes[3] == 'N' || firstBytes[3] == 'n') &&
                  (firstBytes[4] == 'E' || firstBytes[4] == 'e') &&
                  (firstBytes[5] == 'C' || firstBytes[5] == 'c') &&
                  (firstBytes[6] == 'T' || firstBytes[6] == 't')))
            {
                return false;
            }

            // Find pattern: (\r?\n){2}
            int lastPos;
            for (lastPos = "CONNECT".Length; lastPos < firstBytes.Count; ++lastPos)
            {
                if (firstBytes[lastPos] == '\n')
                {
                    if ((firstBytes[lastPos - 1] == '\n') ||
                        (firstBytes[lastPos - 1] == '\r' && (firstBytes[lastPos - 2] == '\n')))
                    {
                        break;
                    }
                }
            }
            
            // If we didn't find (\r?\n){2}, just go on
            if (lastPos == firstBytes.Count)
            {
                // Anyway, the HTTP header can't be too looooong (>64KB)
                if (firstBytes.Count > 1024 * 64)
                {
                    return false;
                }
                return null;
            }

            string header = Encoding.ASCII.GetString(firstBytes.ToArray(), 0, lastPos + 1);
            Match match = s_RegexHttpHeaders.Match(header);
            string httpVersion = match.Groups["HTTP_VERSION"].Value;
            Console.WriteLine($"Happily accept a proxy connection: HTTP");
            Console.WriteLine(match.Success);
            Console.WriteLine($"Connect - {match.Groups["HOSTNAME"].Value}");
            Console.WriteLine($"Http    - {httpVersion}");
            foreach (Capture capture in match.Groups["HEADERS"].Captures)
            {
                Console.WriteLine($"Header  - {capture.Value}");
            }

            byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\n\r\n");
            //this.StartSend(response, 0, response.Length);
            
            return true;
        }
        
        public HttpProxyListener(ProxyServer server, Socket connectedSocket) : base(server, connectedSocket)
        {
        }

        public override void OnReceiveData(byte[] buffer, int offset, int count)
        {
        }

        public override void OnCloseConnection(SocketError status)
        {
            
        }
    }
}