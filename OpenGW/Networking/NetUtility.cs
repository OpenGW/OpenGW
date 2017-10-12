using System.Text.RegularExpressions;

namespace OpenGW.Networking
{
    public static class NetUtility
    {
        private static Regex s_RegexEndpoint = 
            new Regex(@"^(?<HOST>([^:]+)|(\[[A-Fa-z0-9:]+\]))(:(?<PORT>[0-9]+))?$",
                RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
        
        public static bool TrySplitHostAndPort(string endpoint, out string hostOrIp, out int? port)
        {
            hostOrIp = null;
            port = null;
            
            Match match = s_RegexEndpoint.Match(endpoint);
            if (!match.Success)
            {
                return false;
            }

            string strPort = match.Groups["PORT"].Value;
            if (string.IsNullOrEmpty(strPort))
            {
                hostOrIp = match.Groups["HOST"].Value;
                port = null;
                return true;
            }
            else
            {
                if (int.TryParse(strPort, out int intPort))
                {
                    hostOrIp = match.Groups["HOST"].Value;
                    port = intPort;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}