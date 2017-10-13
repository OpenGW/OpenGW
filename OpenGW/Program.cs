using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using OpenGW.Networking;
using OpenGW.Proxy;

namespace OpenGW
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            Logger.MinLogLevel = LogLevel.Debug;
            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.IPv6Loopback, 10080);

            ProxyConfiguration configuration = new ProxyConfiguration(endPoint);
            
            ProxyServer proxyServer = new ProxyServer(configuration);
            proxyServer.Start();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
            
            Console.WriteLine("Exit!");
            proxyServer.Stop();
            Thread.Sleep(100);
        }
    }
}
