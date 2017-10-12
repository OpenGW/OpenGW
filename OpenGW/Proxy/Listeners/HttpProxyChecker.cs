using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenGW.Proxy
{
    public class HttpProxyChecker : AbstractProxyChecker
    {
        
        private static readonly Regex s_RegexHttpHeaders 
            = new Regex(@"^CONNECT(\s+)(?<HOSTNAME>\S+)(\s+)HTTP/(?<HTTP_VERSION>[0-9\.]+)(\s*\r?\n)((?<HEADERS>[^\n]+?)\r?\n)*(\r?\n)$",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
        
        public static ProxyCheckerResult TryThisProxy(List<byte> firstBytes)
        {
            const string CONNECT_UPPER = "CONNECT";
            const string CONNECT_LOWER = "connect";

            for (int i = Math.Min(CONNECT_UPPER.Length, firstBytes.Count) - 1; i >= 0; --i)
            {
                if (firstBytes[i] != (byte)CONNECT_UPPER[i] && firstBytes[i] != (byte)CONNECT_LOWER[i])
                {
                    return ProxyCheckerResult.Failed;
                }
            }
            
            return (firstBytes.Count < CONNECT_UPPER.Length) 
                ? ProxyCheckerResult.Uncertain 
                : ProxyCheckerResult.Success;
        }



        private int m_LastScanPosition = 0;
        
        public override ProxyCheckerResult Initialize(List<byte> firstBytes, out AbstractProxyListener proxyListener, out int unusedBytes)
        {
            Debug.Assert(firstBytes.Count >= "CONNECT".Length);
            
            // Default values (failed or uncertain)
            proxyListener = null;
            unusedBytes = 0;
            

            // Find pattern: (\r?\n){2}
            if (this.m_LastScanPosition == 0)
            {
                this.m_LastScanPosition = "CONNECT".Length;
            }
            for (; this.m_LastScanPosition < firstBytes.Count; ++this.m_LastScanPosition)
            {
                if (firstBytes[this.m_LastScanPosition] == '\n')
                {
                    if ((firstBytes[this.m_LastScanPosition - 1] == '\n') ||
                        (firstBytes[this.m_LastScanPosition - 1] == '\r' && (firstBytes[this.m_LastScanPosition - 2] == '\n')))
                    {
                        break;
                    }
                }
            }
            
            // If we didn't find (\r?\n){2}, just go on
            if (this.m_LastScanPosition == firstBytes.Count)
            {                
                // Anyway, the HTTP header can't be too looooong (eg, > 256KB)
                return (firstBytes.Count > 1024 * 256)
                    ? ProxyCheckerResult.Failed 
                    : ProxyCheckerResult.Uncertain;
            }

            string request = Encoding.ASCII.GetString(firstBytes.ToArray(), 0, this.m_LastScanPosition + 1);
            Match match = s_RegexHttpHeaders.Match(request);
            if (!match.Success)
            {
                return ProxyCheckerResult.Failed;
            }
            
            string httpVersion = match.Groups["HTTP_VERSION"].Value;
            string hostname = match.Groups["HOSTNAME"].Value;

            Dictionary<string, string> headers = new Dictionary<string, string>();
            foreach (Capture capture in match.Groups["HEADERS"].Captures)
            {
                int colonIndex = capture.Value.IndexOf(':');
                if (colonIndex < 0)
                {
                    // TODO: Log a warning: invalid header
                    continue;
                }

                string key = capture.Value.Substring(0, colonIndex).Trim();
                string value = capture.Value.Substring(colonIndex + 1).Trim();
                headers[key] = value;
            }
            
            proxyListener = new HttpProxyListener(this.ProxyServer, this.ConnectedSocket, httpVersion, hostname, headers);
            unusedBytes = firstBytes.Count - (this.m_LastScanPosition + 1);
            return ProxyCheckerResult.Success;
        }

        public HttpProxyChecker(ProxyServer proxyServer, Socket connectedSocket) 
            : base(proxyServer, connectedSocket)
        {
        }
    }
}