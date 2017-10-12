using System.Net;
using System.Net.Sockets;

namespace OpenGW.Proxy
{
    public class ProxyConfiguration
    {
        public IPEndPoint ListenEndpoint { get; }
        
        public bool DualModeIfPossible { get; }
        
        public ProxyConfiguration(IPEndPoint listenEndpoint, bool dualModeIfPossible = true)
        {
            this.ListenEndpoint = listenEndpoint;
            this.DualModeIfPossible = dualModeIfPossible;
        }
    }
}