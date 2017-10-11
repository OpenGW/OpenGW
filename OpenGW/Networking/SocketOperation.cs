using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security;

namespace OpenGW.Networking
{
    public static class SocketOperation
    {
        private class SocketUserToken
        {
            public ISocketEvent ClientOrServer;

            /// <summary>
            /// The currently working socket for current operation:
            ///  - Accept:  The server listening socket
            ///  - Connect: The client connecting socket
            ///  - Send:    The server/client connected socket
            ///  - Receive: The server/client connected socket
            /// </summary>
            public Socket Socket;

            public void Cleanup()
            {
                this.ClientOrServer = null;
                this.Socket = null;
            }
        }
        
        
        private const int RECEIVE_BUFFER_LENGTH = 4096;
        
        private static LingerOption LingerStateCloseImmediately { get; } = new LingerOption(true, 0);

        
        private static readonly Pool<SocketAsyncEventArgs> s_ReceivePool;
        private static readonly Pool<SocketAsyncEventArgs> s_SendPool;

        
        static SocketOperation()
        {
            s_ReceivePool = new Pool<SocketAsyncEventArgs>
            {
                Generator = () =>
                {
                    SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
                    saea.Completed += SocketOperation.SocketAsyncEventArgs_OnCompleted;
                    saea.SetBuffer(new byte[RECEIVE_BUFFER_LENGTH], 0, RECEIVE_BUFFER_LENGTH);
                    saea.UserToken = new SocketUserToken();
                    return saea;
                },
                Cleaner = (SocketAsyncEventArgs saea) =>
                {
                    Debug.Assert(saea.AcceptSocket == null);
                    ((SocketUserToken)saea.UserToken).Cleanup();
                }
            };
            
            s_SendPool = new Pool<SocketAsyncEventArgs>
            {
                Generator = () =>
                {
                    SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
                    saea.Completed += SocketOperation.SocketAsyncEventArgs_OnCompleted;
                    saea.UserToken = new SocketUserToken();
                    return saea;
                },
                Cleaner = (SocketAsyncEventArgs saea) =>
                {
                    Debug.Assert(saea.AcceptSocket == null);
                    saea.SetBuffer(null, 0, 0);
                    ((SocketUserToken)saea.UserToken).Cleanup();
                }
            };
        }

        
        private static void SocketAsyncEventArgs_OnCompleted(object sender, SocketAsyncEventArgs saea)
        {
            switch (saea.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    SocketOperation.ProcessAccept(saea);
                    break;
                case SocketAsyncOperation.Connect:
                    SocketOperation.ProcessConnect(saea);
                    break;
                case SocketAsyncOperation.Receive:
                    SocketOperation.ProcessReceive(saea);
                    break;
                case SocketAsyncOperation.Send:
                    SocketOperation.ProcessSend(saea);
                    break;
                //case SocketAsyncOperation.ReceiveFrom:
                //case SocketAsyncOperation.ReceiveMessageFrom:
                //case SocketAsyncOperation.SendTo:
                //case SocketAsyncOperation.SendPackets:
                //case SocketAsyncOperation.Disconnect:
                //case SocketAsyncOperation.None:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        
        private static void ProcessAccept(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Accept);

            SocketUserToken token = (SocketUserToken)saea.UserToken;

            if (saea.SocketError == SocketError.Success)
            {
                // Set the newly accepted socket NOT to linger
                saea.AcceptSocket.LingerState = SocketOperation.LingerStateCloseImmediately;
                
                // Callback: OnAccept()
                token.ClientOrServer.OnAccept(token.Socket, saea.AcceptSocket);
                
                // Start receving on the accepted socket
                SocketOperation.StartReceive(token.ClientOrServer, saea.AcceptSocket);
                
                // Continue to accept new sockets
                SocketOperation.StartAcceptInternal(saea);
            }
            else if (saea.SocketError == SocketError.OperationAborted)
            {
                // Callback: OnCloseListener()
                token.ClientOrServer.OnCloseListener(token.Socket, saea.SocketError);
                
                // Now the listener socket is closed. Stop accepting.
                SocketOperation.DisposeSocketAsyncEventArgs(saea);
                
                // UNCERTAIN: I shouldn't dispose token.Socket (the listener) here?
            }
            else
            {
                // MSDN:
                // The SocketAsyncEventArgs.Completed event can occur in some cases when no connection 
                // has been accepted and cause the SocketAsyncEventArgs.SocketError property to be set 
                // to ConnectionReset. This can occur as a result of port scanning using a half-open 
                // SYN type scan (a SYN -> SYN-ACK -> RST sequence). 
                // Applications using the AcceptAsync method should be prepared to handle this condition.
                
                // Now an error happened during accepting
                token.ClientOrServer.OnAcceptError(token.Socket, saea.SocketError);
                
                // Continue to accept new sockets
                SocketOperation.StartAcceptInternal(saea);
            }
        }

        private static void ProcessConnect(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Connect);

            SocketUserToken token = (SocketUserToken)saea.UserToken;

            if (saea.SocketError == SocketError.Success)
            {
                Debug.Assert(object.ReferenceEquals(saea.ConnectSocket, token.Socket));
                token.ClientOrServer.OnConnect(token.Socket);
                
                // Start receving on the accepted socket
                SocketOperation.StartReceive(token.ClientOrServer, token.Socket);
            }
            else
            {
                token.ClientOrServer.OnConnectError(token.Socket, saea.SocketError);
                                
                // TODO: Should I dispose token.Socket here?
            }
            
            // Now the listener socket is closed. Stop accepting.
            SocketOperation.DisposeSocketAsyncEventArgs(saea);
        }

        private static void ProcessReceive(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Receive);

            SocketUserToken token = (SocketUserToken)saea.UserToken;

            if (saea.SocketError == SocketError.Success)
            {
                if (saea.BytesTransferred > 0)
                {
                    token.ClientOrServer.OnReceive(token.Socket, saea.Buffer, saea.Offset, saea.BytesTransferred);
                    
                    SocketOperation.StartReceiveInternal(saea);
                    return;
                }
                else
                {
                    // The connection is closed by remote
                }
            }
            else
            {
                // Now an error happened during receiving
                token.ClientOrServer.OnReceiveError(token.Socket, saea.SocketError);
            }
            
            // Close the socket (OnCloseConnection() will be called)
            SocketOperation.CloseSocketConnection(token, saea.SocketError);
            
            // Recycle the SocketAsyncEventArgs
            s_ReceivePool.Push(saea);
        }
        
        private static void ProcessSend(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Send);

            SocketUserToken token = (SocketUserToken)saea.UserToken;

            if (saea.SocketError == SocketError.Success)
            {
                // Is this right? 
                // If I tried to send something (not empty) and got the callback, at least 1 byte is sent
                Debug.Assert(saea.BytesTransferred > 0 || saea.Count == 0);
                
                // Callback - OnSend event
                token.ClientOrServer.OnSend(token.Socket, saea.Buffer, saea.Offset, saea.BytesTransferred);
            }
            else
            {
                // Now an error happened during sending
                token.ClientOrServer.OnSendError(token.Socket, saea.SocketError);
            
                // Close the socket (OnCloseConnection() will be called)
                SocketOperation.CloseSocketConnection(token, saea.SocketError);
            }
            
            // Recycle the SocketAsyncEventArgs
            s_SendPool.Push(saea);
        }

        
        private static void StartAcceptInternal(SocketAsyncEventArgs saea)
        {
            SocketUserToken token = (SocketUserToken)saea.UserToken;

            SocketError error;
            bool isAsync = false;
            try
            {
                saea.AcceptSocket = null;

                // MSDN documentation:
                // https://msdn.microsoft.com/en-us/library/system.net.sockets.socket.acceptasync(v=vs.110).aspx
                isAsync = token.Socket.AcceptAsync(saea);
                error = SocketError.Success;
            }
            catch (SocketException ex)
            {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                error = ex.SocketErrorCode;

                if (error == SocketError.OperationAborted)
                {
                    // SocketError.OperationAborted if listener is closed
                    // Callback: OnCloseListener()
                    token.ClientOrServer.OnCloseListener(token.Socket, ex.SocketErrorCode);

                    // Dispose the SocketAsyncEventArgs
                    SocketOperation.DisposeSocketAsyncEventArgs(saea);

                    // UNCERTAIN: I shouldn't dispose token.Socket (the listener) here?
                }
                else
                {
                    // MSDN:
                    // The SocketAsyncEventArgs.Completed event can occur in some cases when no connection 
                    // has been accepted and cause the SocketAsyncEventArgs.SocketError property to be set 
                    // to ConnectionReset. This can occur as a result of port scanning using a half-open 
                    // SYN type scan (a SYN -> SYN-ACK -> RST sequence). 
                    // Applications using the AcceptAsync method should be prepared to handle this condition.
                    token.ClientOrServer.OnAcceptError(token.Socket, ex.SocketErrorCode);
                
                    // UNCERTAIN: try accept again, not just close the listener?
                    SocketOperation.StartAcceptInternal(saea);
                }
            }
            
            
            if (error == SocketError.Success && !isAsync)
            {
                SocketOperation.ProcessAccept(saea);
            }
        }

        public static void StartAccept(ISocketEvent server, Socket listenerSocket)
        {
            // Set the listener NOT to linger
            listenerSocket.LingerState = SocketOperation.LingerStateCloseImmediately;
            
            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += SocketOperation.SocketAsyncEventArgs_OnCompleted;

            saea.UserToken = new SocketUserToken
            {
                ClientOrServer = server,
                Socket = listenerSocket,
            };

            Debug.Assert(saea.AcceptSocket == null);
            
            SocketOperation.StartAcceptInternal(saea);
        }


        public static void StartConnect(ISocketEvent client, Socket clientSocket, IPEndPoint remoteEp)
        {
            // Set the client NOT to linger
            clientSocket.LingerState = SocketOperation.LingerStateCloseImmediately;

            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += SocketOperation.SocketAsyncEventArgs_OnCompleted;

            saea.UserToken = new SocketUserToken
            {
                ClientOrServer = client,
                Socket = clientSocket,
            };
            
            
            SocketError error;
            bool isAsync = false;
            try
            {
                // MSDN documentation: (Socket.ConnectAsync Method)
                // https://msdn.microsoft.com/en-us/library/bb538102(v=vs.110).aspx

                saea.RemoteEndPoint = remoteEp;
                isAsync = clientSocket.ConnectAsync(saea);
                error = SocketError.Success;
            }
            catch (SocketException ex)
            {
                // MSDN:
                // An error occurred when attempting to access the socket.
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                error = ex.SocketErrorCode;
            }


            if (error == SocketError.Success && !isAsync)
            {
                SocketOperation.ProcessConnect(saea);
            }
            else if (error != SocketError.Success)
            {
                client.OnConnectError(clientSocket, error);
                SocketOperation.DisposeSocketAsyncEventArgs(saea);
            }
        }



        private static void StartReceiveInternal(SocketAsyncEventArgs saea)
        {
            SocketUserToken token = (SocketUserToken)saea.UserToken;

            SocketError error;
            bool isAsync = false;
            try
            {
                isAsync = token.Socket.ReceiveAsync(saea);
                error = SocketError.Success;
            }
            catch (SocketException ex)
            {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                error = ex.SocketErrorCode;
            }

            
            if (error == SocketError.Success && !isAsync)
            {
                SocketOperation.ProcessReceive(saea);
            }
            else if (error != SocketError.Success)
            {
                token.ClientOrServer.OnReceiveError(token.Socket, error);

                SocketOperation.CloseSocketConnection(token, error);
            }
        }

        private static void StartReceive(ISocketEvent clientOrServer, Socket socket)
        {
            SocketAsyncEventArgs saea = s_ReceivePool.Pop();
            SocketUserToken token = (SocketUserToken)saea.UserToken;
            token.ClientOrServer = clientOrServer;
            token.Socket = socket;
            
            SocketOperation.StartReceiveInternal(saea);
        }

        
        public static void StartSend(ISocketEvent clientOrServer, Socket socket, byte[] buffer, int offset, int count)
        {
            SocketAsyncEventArgs saea = s_SendPool.Pop();
            
            SocketUserToken token = (SocketUserToken)saea.UserToken;
            token.ClientOrServer = clientOrServer;
            token.Socket = socket;
            
            SocketError error;
            bool isAsync = false;
            try
            {
                saea.SetBuffer(buffer, offset, count);
                
                isAsync = token.Socket.SendAsync(saea);
                error = SocketError.Success;
            }
            catch (SocketException ex)
            {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                error = ex.SocketErrorCode;
            }

            
            if (error == SocketError.Success && !isAsync)
            {
                SocketOperation.ProcessSend(saea);
            }
            else if (error != SocketError.Success)
            {
                token.ClientOrServer.OnSendError(token.Socket, error);
                
                SocketOperation.CloseSocketConnection(token, error);
            }
        }

    
        
        private static void CloseSocketConnection(SocketUserToken token, SocketError error)
        {
            try
            {
                token.Socket.Dispose();
            }
            catch (SocketException ex)
            {
                error = ex.SocketErrorCode;
            }
            catch
            {
                // Ignore
                // Don't change the SocketError code
            }
            finally
            {
                token.ClientOrServer.OnCloseConnection(token.Socket, error);
            }
        }

        private static void DisposeSocketAsyncEventArgs(SocketAsyncEventArgs saea)
        {
            ((SocketUserToken)saea.UserToken).Cleanup();
            saea.Dispose();
        }
        
    }
}
