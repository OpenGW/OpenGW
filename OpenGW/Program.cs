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
        private static int _closeCnt = 0;
        private static int _acceptCnt = 0;
        private static int _connectCnt = 0;
        
        private static void Main(string[] args)
        {
            const int CLIENT_COUNT = 1000;

            var server = GWTcpSocket.CreateServer(new IPEndPoint(IPAddress.IPv6Loopback, 12345));
            server.OnAccept += (gwListener, gwAcceptedSocket) => {
                int cnt = Interlocked.Increment(ref _acceptCnt);
                Console.WriteLine($"{gwListener} OnAccept: {gwAcceptedSocket} ({cnt})");
                
                gwAcceptedSocket.OnReceive += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnReceive: Count = {count}");
                    if (count == 0) {
                        gwSocket.Close();
                    }
                    else {
                        gwSocket.Send(buffer, offset, count);
                    }
                };
                gwAcceptedSocket.OnReceiveError += (gwSocket, error) => {
                    Console.WriteLine($"{gwSocket} OnReceiveError: {error}");
                };
                gwAcceptedSocket.OnSend += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnSend: Count = {count}");
                };
                gwAcceptedSocket.OnSendError += (gwSocket, error, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnSendError: {error} (Count = {count})");
                };
                gwAcceptedSocket.OnCloseConnection += (gwSocket) => {
                    Console.WriteLine($"{gwSocket} OnCloseConnection");
                };

                gwAcceptedSocket.StartReceive();
            };
            server.OnAcceptError += (gwListener, error) => {
                int cnt = Interlocked.Increment(ref _acceptCnt);
                Console.WriteLine($"{gwListener} OnAcceptError: {error} ({cnt})");
            };
            server.OnCloseListener += (gwListener) => {
                Console.WriteLine($"{gwListener} OnCloseListener: {_acceptCnt}");
                Console.WriteLine("Exit!");
                Environment.Exit(0);
            };
            server.StartAccept();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;
            Parallel.For(0, CLIENT_COUNT, options, (id) => {
                Thread.Sleep(id % 10 + 10);

                var client = GWTcpSocket.CreateClient(new IPEndPoint(IPAddress.IPv6Loopback, 12345));
                client.OnReceive += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnReceive: Count = {count}");
                    gwSocket.Close();
                };
                client.OnReceiveError += (gwSocket, error) => {
                    Console.WriteLine($"{gwSocket} OnReceiveError: {error}");
                    gwSocket.Close();
                };
                client.OnSend += (gwSocket, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnSend: Count = {count}");
                };
                client.OnSendError += (gwSocket, error, buffer, offset, count) => {
                    Console.WriteLine($"{gwSocket} OnSendError: {error} (Count = {count})");
                    gwSocket.Close();
                };
                client.OnCloseConnection += (gwSocket) => {
                    int cnt = Interlocked.Increment(ref _closeCnt);
                    Console.WriteLine($"{gwSocket} OnCloseConnection ({cnt})");
                    if (cnt == CLIENT_COUNT) {
                        server.Close();
                    }
                };
                
                client.OnConnect += (gwSocket) => {
                    int cnt = Interlocked.Increment(ref _connectCnt);
                    Console.WriteLine($"{gwSocket} OnConnect ({_connectCnt})");
                    for (int i = 0; i < 1; i++) {
                        client.Send(new byte[124], 0, 124);
                    }
                    client.StartReceive();
                    //client.Close();
                };
                client.OnConnectError += (gwSocket, error) => {
                    int cnt = Interlocked.Increment(ref _connectCnt);
                    Console.WriteLine($"{gwSocket} OnConnectError: {error} ({_connectCnt})");
                    client.Close();

                    //if (_connectCnt == CLIENT_COUNT) {
                    //    server.Close();
                    //}
                };
                
                client.Connect();
            });

            Console.ReadKey(true);
        }
    }
}
