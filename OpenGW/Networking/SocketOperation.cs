using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

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
            public GWSocket GwSocket;

            public void Cleanup()
            {
                this.ClientOrServer = null;
                this.GwSocket = null;
            }
        }
        
        
        private const int RECEIVE_BUFFER_LENGTH = 4096;
        
        private static readonly ConcurrentDictionary<GWSocket, bool> s_ActiveConnectedSockets 
            = new ConcurrentDictionary<GWSocket, bool>();
        
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
                // Callback: OnAccept()
                GWSocket gwAcceptedSocket = new GWSocket(saea.AcceptSocket, GWSocketType.TcpServerConnection);
                gwAcceptedSocket.UpdateEndPointCache();
                token.ClientOrServer.OnAccept(token.GwSocket, gwAcceptedSocket);
                
                // Add accepted socket to active sockets
                s_ActiveConnectedSockets.TryAdd(gwAcceptedSocket, true);  // true is dummy

                // Start receving on the accepted socket
                SocketOperation.AsyncReceive(token.ClientOrServer, gwAcceptedSocket);
                
                // Continue to accept new sockets
                SocketOperation.AsyncAcceptInternal(saea);
            }
            else if (saea.SocketError == SocketError.OperationAborted)
            {
                // Callback: OnCloseListener()
                token.ClientOrServer.OnCloseListener(token.GwSocket, saea.SocketError);
                
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
                token.ClientOrServer.OnAcceptError(token.GwSocket, saea.SocketError);
                
                // Continue to accept new sockets
                SocketOperation.AsyncAcceptInternal(saea);
            }
        }

        private static void ProcessConnect(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Connect);

            SocketUserToken token = (SocketUserToken)saea.UserToken;

            if (saea.SocketError == SocketError.Success)
            {
                Debug.Assert(object.ReferenceEquals(saea.ConnectSocket, token.GwSocket.Socket));
                
                Debug.Assert(token.GwSocket.Type == GWSocketType.TcpClientConnection);
                token.GwSocket.UpdateEndPointCache();
                
                token.ClientOrServer.OnConnect(token.GwSocket);
                
                // Add connected socket to active sockets
                s_ActiveConnectedSockets.TryAdd(token.GwSocket, true);  // true is dummy
                
                // Start receving on the accepted socket
                // Now client tcp receiving must be started manually!
                //SocketOperation.StartClientReceive(token.ClientOrServer, token.GwSocket);
            }
            else
            {
                token.ClientOrServer.OnConnectError(token.GwSocket, saea.SocketError);
                                
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
                    token.ClientOrServer.OnReceive(token.GwSocket, saea.Buffer, saea.Offset, saea.BytesTransferred);
                    
                    SocketOperation.AsyncReceiveInternal(saea);
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
                // OnReceiveError() is removed now
                //token.ClientOrServer.OnReceiveError(token.GwSocket, saea.SocketError);
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
                token.ClientOrServer.OnSend(token.GwSocket, saea.Buffer, saea.Offset, saea.BytesTransferred);
            }
            else
            {
                // Now an error happened during sending
                // OnSendError() is removed now
                //token.ClientOrServer.OnSendError(token.GwSocket, saea.SocketError);
            
                // Close the socket (OnCloseConnection() will be called)
                SocketOperation.CloseSocketConnection(token, saea.SocketError);
            }
            
            // Recycle the SocketAsyncEventArgs
            s_SendPool.Push(saea);
        }

        
        private static void AsyncAcceptInternal(SocketAsyncEventArgs saea)
        {
            SocketUserToken token = (SocketUserToken)saea.UserToken;
            
            Debug.Assert(token.GwSocket.Type == GWSocketType.TcpServerListener);

            SocketError error;
            bool isAsync = false;
            try
            {
                saea.AcceptSocket = null;

                // MSDN documentation:
                // https://msdn.microsoft.com/en-us/library/system.net.sockets.socket.acceptasync(v=vs.110).aspx
                isAsync = token.GwSocket.Socket.AcceptAsync(saea);
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
                    token.ClientOrServer.OnCloseListener(token.GwSocket, ex.SocketErrorCode);

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
                    token.ClientOrServer.OnAcceptError(token.GwSocket, ex.SocketErrorCode);
                
                    // UNCERTAIN: try accept again, not just close the listener?
                    SocketOperation.AsyncAcceptInternal(saea);
                }
            }
            
            
            if (error == SocketError.Success && !isAsync)
            {
                SocketOperation.ProcessAccept(saea);
            }
        }

        public static void AsyncAccept(ISocketEvent server, GWSocket listenerGwSocket)
        {
            Debug.Assert(listenerGwSocket.Type == GWSocketType.TcpServerListener);
            listenerGwSocket.UpdateEndPointCache();  // store LocalEndPoint

            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += SocketOperation.SocketAsyncEventArgs_OnCompleted;

            saea.UserToken = new SocketUserToken
            {
                ClientOrServer = server,
                GwSocket = listenerGwSocket,
            };

            Debug.Assert(saea.AcceptSocket == null);

            try
            {
                SocketOperation.AsyncAcceptInternal(saea);
            }
            catch
            {
                // There are some exceptions: 
                //   InvalidOperationException, ObjectDisposedException, SecurityException, ...
                // Don't know how to deal with them: just throw
                SocketOperation.DisposeSocketAsyncEventArgs(saea);
                throw;
            }
        }


        public static void AsyncConnect(ISocketEvent client, GWSocket clientSocket, IPEndPoint remoteEp)
        {
            Debug.Assert(clientSocket.Type == GWSocketType.TcpClientConnection);
            
            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += SocketOperation.SocketAsyncEventArgs_OnCompleted;

            saea.UserToken = new SocketUserToken
            {
                ClientOrServer = client,
                GwSocket = clientSocket,
            };
            
            
            SocketError error;
            bool isAsync = false;
            try
            {
                // MSDN documentation: (Socket.ConnectAsync Method)
                // https://msdn.microsoft.com/en-us/library/bb538102(v=vs.110).aspx

                saea.RemoteEndPoint = remoteEp;
                isAsync = clientSocket.Socket.ConnectAsync(saea);
                error = SocketError.Success;
            }
            catch (SocketException ex)
            {
                // MSDN:
                // An error occurred when attempting to access the socket.
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                error = ex.SocketErrorCode;
            }
            catch
            {
                // There are other exceptions: 
                //   InvalidOperationException, ObjectDisposedException, SecurityException, ...
                // Don't know how to deal with them: just throw
                SocketOperation.DisposeSocketAsyncEventArgs(saea);
                throw;
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



        private static void AsyncReceiveInternal(SocketAsyncEventArgs saea)
        {
            SocketUserToken token = (SocketUserToken)saea.UserToken;

            Debug.Assert(token.GwSocket.Type == GWSocketType.TcpServerConnection ||
                         token.GwSocket.Type == GWSocketType.TcpClientConnection);

            SocketError error;
            bool isAsync = false;
            bool disposed = false;
            try
            {
                isAsync = token.GwSocket.Socket.ReceiveAsync(saea);
                error = SocketError.Success;
            }
            catch (SocketException ex)
            {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                error = ex.SocketErrorCode;
            }
            catch (ObjectDisposedException)
            {
                error = SocketError.SocketError;
                disposed = true;
            }
            
            if (error == SocketError.Success && !isAsync)
            {
                SocketOperation.ProcessReceive(saea);
            }
            else if (error != SocketError.Success)
            {
                if (!disposed)  // not due to ObjectDisposedException
                {
                    // OnReceiveError() is removed now
                    //token.ClientOrServer.OnReceiveError(token.GwSocket, error);
                }

                SocketOperation.CloseSocketConnection(token, error);
            }
        }

        private static void AsyncReceive(ISocketEvent clientOrServer, GWSocket gwSocket)
        {
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnection ||
                         gwSocket.Type == GWSocketType.TcpClientConnection);

            SocketAsyncEventArgs saea = s_ReceivePool.Pop();
            SocketUserToken token = (SocketUserToken)saea.UserToken;
            token.ClientOrServer = clientOrServer;
            token.GwSocket = gwSocket;
            
            SocketOperation.AsyncReceiveInternal(saea);
        }

        public static void StartClientReceive(ISocketEvent clientOrServer, GWSocket gwSocket)
        {
            Debug.Assert(gwSocket.Type == GWSocketType.TcpClientConnection);

            SocketOperation.AsyncReceive(clientOrServer, gwSocket);
        }
        
        public static void AsyncSend(ISocketEvent clientOrServer, GWSocket gwSocket, byte[] buffer, int offset, int count)
        {
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnection || 
                         gwSocket.Type == GWSocketType.TcpClientConnection);
            
            
            SocketAsyncEventArgs saea = s_SendPool.Pop();
            
            SocketUserToken token = (SocketUserToken)saea.UserToken;
            token.ClientOrServer = clientOrServer;
            token.GwSocket = gwSocket;
            
            SocketError error;
            bool isAsync = false;
            try
            {
                saea.SetBuffer(buffer, offset, count);

                isAsync = token.GwSocket.Socket.SendAsync(saea);
                error = SocketError.Success;
            }
            catch (SocketException ex)
            {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                error = ex.SocketErrorCode;
            }
            catch (ObjectDisposedException)
            {
                error = SocketError.SocketError;
            }

            
            if (error == SocketError.Success && !isAsync)
            {
                SocketOperation.ProcessSend(saea);
            }
            else if (error != SocketError.Success)
            {
                // OnSendError() is removed now
                //token.ClientOrServer.OnSendError(token.GwSocket, error);
                
                SocketOperation.CloseSocketConnection(token, error);
            }
        }

    
        
        private static void CloseSocketConnection(SocketUserToken token, SocketError error)
        {
            if (!s_ActiveConnectedSockets.TryRemove(token.GwSocket, out bool dummy))
            {
                // This socket is not in the active sockets set.
                // It must have been called CloseSocketConnection()
                return;
            }
            
            try
            {
                token.GwSocket.Close();
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
                token.ClientOrServer.OnCloseConnection(token.GwSocket, error);
            }
        }

        private static void DisposeSocketAsyncEventArgs(SocketAsyncEventArgs saea)
        {
            ((SocketUserToken)saea.UserToken).Cleanup();
            saea.Dispose();
        }
        
    }
}
