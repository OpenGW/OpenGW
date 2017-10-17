using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenGW.Networking;

namespace OpenGW
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            const int CLIENT_COUNT = 10;

            var server = GWTcpSocket.CreateServer(new IPEndPoint(IPAddress.Loopback, 12345));
            server.OnAccept += (gwListener, gwAcceptedSocket) => {
                Console.WriteLine("[S@][OnAccept]");
                
                gwAcceptedSocket.OnReceive += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"[S>][OnReceive] Count = {count}");
                    if (count == 0) {
                        gwSocket.Close();
                    }
                };
                gwAcceptedSocket.OnReceiveError += (gwSocket, error) => {
                    Console.WriteLine($"[S>][OnReceiveError] Error = {error}");
                };
                gwAcceptedSocket.OnSend += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"[S>][OnSend] Count = {count}");
                };
                gwAcceptedSocket.OnSendError += (gwSocket, error, buffer, offset, count) => {
                    Console.WriteLine($"[S>][OnSendError] Error = {error} (Count = {count})");
                };
                gwAcceptedSocket.OnCloseConnection += (gwSocket) => {
                    Console.WriteLine($"[S>][OnCloseConnection]");
                };

                gwAcceptedSocket.StartReceive();
            };
            server.OnAcceptError += (gwListener, error) => {
                Console.WriteLine($"[S@][OnAcceptError] {error}");
            };
            server.OnCloseListener += (gwListener) => {
                Console.WriteLine($"[S@][OnCloseListener]");
            };
            server.StartAccept();

            Parallel.For(0, CLIENT_COUNT, (id) => {
                var client = GWTcpSocket.CreateClient(new IPEndPoint(IPAddress.Loopback, 12345));
                client.OnReceive += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"[C>][OnReceive] Count = {count}");
                    //if (count == 0) {
                    //    gwSocket.Close();
                    //}
                };
                client.OnReceiveError += (gwSocket, error) => {
                    Console.WriteLine($"[C>][OnReceiveError] Error = {error}");
                };
                client.OnSend += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"[C>][OnSend] Count = {count}");
                };
                client.OnSendError += (gwSocket, error, buffer, offset, count) => {
                    Console.WriteLine($"[C>][OnSendError] Error = {error} (Count = {count})");
                };
                client.OnCloseConnection += (gwSocket) => {
                    Console.WriteLine($"[C>][OnCloseConnection]");
                };
                client.Connect(0);

                client.StartReceive();

                for (int i = 0; i < 10; i++) {
                    client.Send(new byte[124], 0, 124);
                }
                client.Close();
            });

            server.Close();

            Console.ReadKey(true);
            Console.WriteLine("Exit!");
        }
    }
}
