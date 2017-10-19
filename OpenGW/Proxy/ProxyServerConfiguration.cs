using System.Net;

namespace OpenGW.Proxy
{
    public class ProxyServerConfiguration
    {
        public IPEndPoint BindEndPoint { get; }

        public bool DualModeIfPossible { get; }
        
        
        public ProxyServerConfiguration()
        {
            this.BindEndPoint = new IPEndPoint(IPAddress.IPv6Any, 10080);
            this.DualModeIfPossible = true;
        }
    }
}