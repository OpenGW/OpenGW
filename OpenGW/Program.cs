using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenGW.Networking;

namespace OpenGW
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            const int CLIENT_COUNT = 100;

            var server = GWTcpSocket.CreateServer(new IPEndPoint(IPAddress.IPv6Loopback, 12345));
            server.OnAccept += (gwListener, gwAcceptedSocket) => {
                Console.WriteLine($"{gwListener} OnAccept: {gwAcceptedSocket}");
                
                gwAcceptedSocket.OnReceive += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"{gwListener} OnReceive: Count = {count}");
                    if (count == 0) {
                        gwSocket.Close();
                    }
                };
                gwAcceptedSocket.OnReceiveError += (gwSocket, error) => {
                    Console.WriteLine($"{gwListener} OnReceiveError: {error}");
                };
                gwAcceptedSocket.OnSend += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"{gwListener} OnSend: Count = {count}");
                };
                gwAcceptedSocket.OnSendError += (gwSocket, error, buffer, offset, count) => {
                    Console.WriteLine($"{gwListener} OnSendError: {error} (Count = {count})");
                };
                gwAcceptedSocket.OnCloseConnection += (gwSocket) => {
                    Console.WriteLine($"{gwListener} OnCloseConnection");
                };

                gwAcceptedSocket.StartReceive();
            };
            server.OnAcceptError += (gwListener, error) => {
                Console.WriteLine($"{gwListener} OnAcceptError: {error}");
            };
            server.OnCloseListener += (gwListener) => {
                Console.WriteLine($"{gwListener} OnCloseListener");
            };
            server.StartAccept();

            Parallel.For(0, CLIENT_COUNT, (id) => {
                //Thread.Sleep(id * 2);

                var client = GWTcpSocket.CreateClient(new IPEndPoint(IPAddress.IPv6Loopback, 12345));
                client.OnReceive += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnReceive: Count = {count}");
                    //if (count == 0) {
                    //    gwSocket.Close();
                    //}
                };
                client.OnReceiveError += (gwSocket, error) => {
                    Console.WriteLine($"{gwSocket} OnReceiveError: {error}");
                };
                client.OnSend += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnSend: Count = {count}");
                };
                client.OnSendError += (gwSocket, error, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnSendError: {error} (Count = {count})");
                };
                client.OnCloseConnection += (gwSocket) => {
                    Console.WriteLine($"{gwSocket} OnCloseConnection");
                };

                try {
                    client.Connect(0);
                }
                catch (SocketException ex) {
                    Console.WriteLine(ex.SocketErrorCode);
                    return;
                }

                for (int i = 0; i < 1; i++) {
                    client.Send(new byte[124], 0, 124);
                }

                client.StartReceive();

                client.Close();
            });

            server.Close();

            Console.ReadKey(true);
            Console.WriteLine("Exit!");
        }
    }
}
