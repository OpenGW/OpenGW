﻿using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using OpenGW.Networking;

namespace OpenGW
{
    public static class Program
    {
        class ClientServer : ISocketEvent
        {
            private readonly string m_Tag;

            public ClientServer(string tag)
            {
                this.m_Tag = tag;
            }
            
            public void OnAccept(Socket listener, Socket acceptSocket)
            {
                Console.WriteLine($"[{this.m_Tag}:Accept]");
            }

            public void OnAcceptError(Socket listener, SocketError error)
            {
                Console.WriteLine($"[{this.m_Tag}:AcceptError] {error}");
            }

            public void OnConnect(Socket connectedSocket)
            {
                Console.WriteLine($"[{this.m_Tag}:Connect]");
            }

            public void OnConnectError(Socket socket, SocketError error)
            {
                Console.WriteLine($"[{this.m_Tag}:ConnectError] {error}");
            }

            private int m_RecvCnt = 0;
            private Stopwatch m_Stopwatch;
            
            public void OnReceive(Socket socket, byte[] buffer, int offset, int count)
            {
                if (this.m_RecvCnt == 0)
                {
                    this.m_Stopwatch = Stopwatch.StartNew();
                }
                
                this.m_RecvCnt += count;
                if (count < 4096)  // This is the last packet
                {
                    Console.WriteLine($"[{this.m_Tag}:Receive] Offset = {offset}, Count = {count}, Total = {this.m_RecvCnt}, Time = {this.m_Stopwatch.Elapsed}");
                }
            }

            public void OnReceiveError(Socket socket, SocketError error)
            {
                Console.WriteLine($"[{this.m_Tag}:ReceiveError] {error}");
            }

            public void OnSend(Socket socket, byte[] buffer, int offset, int count)
            {
                Console.WriteLine($"[{this.m_Tag}:Send] Offset = {offset}, Count = {count}");
            }

            public void OnSendError(Socket socket, SocketError error)
            {
                Console.WriteLine($"[{this.m_Tag}:SendError] {error}");
            }

            public void OnCloseConnection(Socket socket, SocketError error)
            {
                Console.WriteLine($"[{this.m_Tag}:CloseConnection] {error}");
            }

            public void OnCloseListener(Socket socket, SocketError error)
            {
                Console.WriteLine($"[{this.m_Tag}:CloseListener] {error}");
            }
        }
        
        
        private static void Main(string[] args)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 10256);
            
            Socket serverSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(endPoint);
            serverSocket.Listen(0);
            
            Socket clientSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            //
            // To connect/accept
            //
            ClientServer server = new ClientServer("Server");
            SocketOperation.StartAccept(server, serverSocket);
                        
            ClientServer client = new ClientServer("Client");
            SocketOperation.StartConnect(client, clientSocket, endPoint);
            
            Thread.Sleep(100);  // I think 100ms enough to establish a TCP connection
            
            
            //
            // To send/receive
            //
            const int OFFSET = 10;
            byte[] buffer = new byte[OFFSET + 1000 * 1000 * 1000];
            SocketOperation.StartSend(client, clientSocket, buffer, OFFSET, buffer.Length - OFFSET);

            Thread.Sleep(1000);
            
            
            //
            // To close
            //
            clientSocket.Shutdown(SocketShutdown.Both);            
            clientSocket.Dispose();
            
            serverSocket.Dispose();
            
            Thread.Sleep(1000);
            Console.WriteLine("Exit!");
        }
    }
}
