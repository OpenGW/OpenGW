﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace OpenGW.Networking
{
    public partial class GWTcpSocket : GWSocket
    {
        public delegate void OnAcceptDelegate(GWTcpSocket gwListener, GWTcpSocket gwAcceptedSocket);
        public delegate void OnAcceptErrorDelegate(GWTcpSocket gwListener, SocketError error);
        public delegate void OnConnectDelegate(GWTcpSocket gwConnectedSocket);
        public delegate void OnConnectErrorDelegate(GWTcpSocket gwSocket, SocketError error);
        public delegate void OnReceiveDelegate(GWTcpSocket gwSocket, byte[] buffer, int offset, int count);
        public delegate void OnReceiveErrorDelegate(GWTcpSocket gwSocket, SocketError error);
        public delegate void OnSendDelegate(GWTcpSocket gwSocket, byte[] buffer, int offset, int count);
        public delegate void OnSendErrorDelegate(GWTcpSocket gwSocket, SocketError error, byte[] buffer, int offset, int count);
        public delegate void OnCloseListenerDelegate(GWTcpSocket gwSocket);
        public delegate void OnCloseConnectionDelegate(GWTcpSocket gwSocket);

        public OnAcceptDelegate OnAccept;
        public OnAcceptErrorDelegate OnAcceptError;
        public OnConnectDelegate OnConnect;
        public OnConnectErrorDelegate OnConnectError;
        public OnReceiveDelegate OnReceive;
        public OnReceiveErrorDelegate OnReceiveError;
        public OnSendDelegate OnSend;
        public OnSendErrorDelegate OnSendError;
        public OnCloseListenerDelegate OnCloseListener;
        public OnCloseConnectionDelegate OnCloseConnection;

        public GWTcpSocket TcpServer { get; } = null;

        //
        // For TcpServerListener only
        //
        private readonly Action<Socket> _acceptedSocketPropertySetter = null;

        //
        // For TcpClientConnector only
        //
        private readonly IPEndPoint _connectEndPoint = null;
        private int _connectInvoked = 0;
        

        /// <summary>
        /// Create a new TCP client connector
        /// </summary>
        private GWTcpSocket(
            IPEndPoint connectEndPoint,
            Action<Socket> socketPropertySetter)
            : base(
                new Socket(connectEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp),
                GWSocketType.TcpClientConnector,
                socketPropertySetter)
        {
            this._connectEndPoint = connectEndPoint;
        }

        /// <summary>
        /// Create a new TCP server listener
        /// </summary>
        private GWTcpSocket(
            IPEndPoint bindEndPoint,
            Action<Socket> socketPropertySetter,
            Action<Socket> acceptedSocketPropertySetter)
            : base(
                new Socket(bindEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp),
                GWSocketType.TcpServerListener,
                socketPropertySetter)
        {
            this._acceptedSocketPropertySetter = acceptedSocketPropertySetter;

            this.Socket.Bind(bindEndPoint);
            this.Socket.Listen(0);

            this.LocalEndPoint = (IPEndPoint)this.Socket.LocalEndPoint;
        }

        /// <summary>
        /// Create a new TCP server connector
        /// </summary>
        private GWTcpSocket(
            Socket acceptedSocket,
            GWTcpSocket gwListener)
            : base(
                  acceptedSocket,
                  GWSocketType.TcpServerConnector,
                  gwListener._acceptedSocketPropertySetter)
        {
            this.TcpServer = gwListener;

            this.LocalEndPoint = (IPEndPoint)this.Socket.LocalEndPoint;
            this.RemoteEndPoint = (IPEndPoint)this.Socket.RemoteEndPoint;

        }


        public static GWTcpSocket CreateServer(
            IPEndPoint bindEndPoint,
            Action<Socket> socketPropertySetter = null,
            Action<Socket> acceptedSocketPropertySetter = null)
        {
            return new GWTcpSocket(bindEndPoint, socketPropertySetter, acceptedSocketPropertySetter);
        }

        public static GWTcpSocket CreateClient(
            IPEndPoint connectEndPoint,
            Action<Socket> socketPropertySetter = null)
        {
            return new GWTcpSocket(connectEndPoint, socketPropertySetter);
        }


        /*
        public void Connect(int msTimeout)
        {
            if (this.Type != GWSocketType.TcpClientConnector) {
                throw new InvalidOperationException($"Can't use Connect on {this.Type}");
            }


            if (this._connectionStatus != CONNECT_NOT_ATTEMPTED) {
                return;
            }
            this._connectionStatus = CONNECT_FAILED;


            if (msTimeout == 0) {
                this.Socket.Connect(this._connectEndPoint);
            }
            else {
                IAsyncResult result = this.Socket.BeginConnect(this._connectEndPoint, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(msTimeout);
                if (success) {
                    this.Socket.EndConnect(result);  // may throw exception
                }
                else {
                    this.Socket.Close();
                    throw new SocketException((int)SocketError.TimedOut);
                }
            }

            this.LocalEndPoint = (IPEndPoint)this.Socket.LocalEndPoint;
            this.RemoteEndPoint = (IPEndPoint)this.Socket.RemoteEndPoint;

            this._connectionStatus = CONNECT_SUCCESSFUL;
        }
        */

        protected override void InvokeCloseCallback()
        {
            switch (this.Type) {
                case GWSocketType.TcpServerListener:
                    this.OnCloseListener?.Invoke(this);
                    break;
                case GWSocketType.TcpServerConnector:
                case GWSocketType.TcpClientConnector:
                    this.OnCloseConnection?.Invoke(this);
                    break;
                case GWSocketType.UdpClient:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override void DoClose(int oldConnectionStatus)
        {
            switch (oldConnectionStatus) {
                case CONNECT_NOT_ATTEMPTED:
                case CONNECT_FAILED:
                case CONNECT_SUCCESSFUL:
                    try {
                        if (this.Type == GWSocketType.TcpClientConnector ||
                            this.Type == GWSocketType.TcpServerConnector) {
                            this.Disconnect();
                            //this.Socket.Close();
                        }
                        else {
                            Debug.Assert(this.Type == GWSocketType.TcpServerListener);
                            this.Socket.Close();
                        }
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.NotConnected) {
                        // Ignore
                    }
                    catch (Exception ex) {
                        Console.WriteLine(ex);
                        this.Socket.Dispose();
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }



        private static void SocketAsyncEventArgs_OnCompleted(object sender, SocketAsyncEventArgs saea)
        {
            switch (saea.LastOperation) {
                case SocketAsyncOperation.Accept:
                    GWTcpSocket.ProcessAccept(saea);
                    break;
                case SocketAsyncOperation.Connect:
                    GWTcpSocket.ProcessConnect(saea);
                    break;
                case SocketAsyncOperation.Disconnect:
                    GWTcpSocket.ProcessDisconnect(saea);
                    break;
                case SocketAsyncOperation.Receive:
                    GWTcpSocket.ProcessReceive(saea);
                    break;
                case SocketAsyncOperation.Send:
                    GWTcpSocket.ProcessSend(saea);
                    break;
                case SocketAsyncOperation.ReceiveFrom:
                case SocketAsyncOperation.SendTo:
                case SocketAsyncOperation.ReceiveMessageFrom:
                case SocketAsyncOperation.SendPackets:
                case SocketAsyncOperation.None:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #region Accept

        private static void CompleteAccept(GWTcpSocket gwListener, SocketAsyncEventArgs saea, SocketError error)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Accept);

            if (error == SocketError.Success) {
                // Now we successfully accepted a new socket
                Debug.Assert(saea.AcceptSocket != null);
                GWTcpSocket gwAcceptedSocket = new GWTcpSocket(saea.AcceptSocket, gwListener);

                // Invoke OnAccept
                // Setting connect status = CONNECT_EVER_SUCCESSFUL
                Debug.Assert(gwAcceptedSocket._connectionStatus == CONNECT_NOT_ATTEMPTED);
                gwAcceptedSocket._connectionStatus = CONNECT_SUCCESSFUL;

                // Increase accepted socket's ActiveOperationCount
                Debug.Assert(gwAcceptedSocket._activeOperationCount == 0);
                Interlocked.Increment(ref gwAcceptedSocket._activeOperationCount);
                Debug.Assert(gwAcceptedSocket._activeOperationCount == 1);

                // Invoke OnAccept()
                gwListener.OnAccept?.Invoke(gwListener, gwAcceptedSocket);

                // Decrease accepted socket's ActiveOperationCount
                Interlocked.Decrement(ref gwAcceptedSocket._activeOperationCount);
                gwAcceptedSocket.CheckClose();
            }
            else {
                // Filter OperationAborted here
                if (error != SocketError.OperationAborted) {
                    gwListener.OnAcceptError?.Invoke(gwListener, error);
                }
            }

            // Decrease listener's ActiveOperationCount
            Interlocked.Decrement(ref gwListener._activeOperationCount);
            bool closeCalled = gwListener.CheckClose();
            if (!closeCalled) {
                // Accept the next socket
                GWTcpSocket.InternalStartAccept(saea);
            }

        }


        private static void ProcessAccept(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Accept);

            GWTcpSocket gwListener = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwListener.Type == GWSocketType.TcpServerListener);

            CompleteAccept(gwListener, saea, saea.SocketError);
        }


        private static void InternalStartAccept(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea != null);
            Debug.Assert(saea.UserToken != null);

            GWTcpSocket gwListener = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwListener.Type == GWSocketType.TcpServerListener);

            Debug.Assert(gwListener._connectionStatus != CONNECT_NOT_ATTEMPTED);
            Debug.Assert(gwListener._connectionStatus != CONNECT_FAILED);

            if (!gwListener.IncreaseActiveOperationCountIfNotClosed()) {

                saea.Dispose();  // TODO: push into pool?
                return;
            }

            saea.AcceptSocket = null;

            try {
                if (!gwListener.Socket.AcceptAsync(saea)) {
                    GWTcpSocket.ProcessAccept(saea);
                }
            }
            catch (SocketException ex) {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);

                CompleteAccept(gwListener, saea, ex.SocketErrorCode);
            }
        }


        public void StartAccept()
        {
            Debug.Assert(this.Type == GWSocketType.TcpServerListener);

            int oldValue = Interlocked.CompareExchange(
                ref this._connectionStatus,
                CONNECT_SUCCESSFUL,
                CONNECT_NOT_ATTEMPTED);
            switch (oldValue) {
                case CONNECT_NOT_ATTEMPTED:
                    // This is normal case
                    break;
                case CONNECT_SUCCESSFUL:
                    // TODO: Log warning
                    Console.WriteLine("This socket has started accepting.");
                    return;
                case CONNECT_CLOSE_PENDING:
                case CONNECT_CLOSED:
                    throw new InvalidOperationException("This socket has called Close()");
                case CONNECT_FAILED:
                    throw new InvalidOperationException("This socket has failed in StartAccept()");
            }

            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += GWTcpSocket.SocketAsyncEventArgs_OnCompleted;
            saea.UserToken = this;

            GWTcpSocket.InternalStartAccept(saea);
        }

        #endregion


        #region Connect

        private static void CompleteConnect(
            GWTcpSocket gwSocket,
            SocketAsyncEventArgs saea,
            SocketError status)
        {
            Debug.Assert(gwSocket._connectionStatus == CONNECT_NOT_ATTEMPTED);
            Debug.Assert(gwSocket._activeOperationCount >= 1);

            if (status == SocketError.Success) {
                gwSocket._connectionStatus = CONNECT_SUCCESSFUL;
                gwSocket.LocalEndPoint = (IPEndPoint)gwSocket.Socket.LocalEndPoint;
                gwSocket.RemoteEndPoint = (IPEndPoint)gwSocket.Socket.RemoteEndPoint;
                gwSocket.OnConnect?.Invoke(gwSocket);
            }
            else {
                gwSocket._connectionStatus = CONNECT_FAILED;
                gwSocket.OnConnectError?.Invoke(gwSocket, status);
            }

            Interlocked.Decrement(ref gwSocket._activeOperationCount);
            bool closeCalled = gwSocket.CheckClose();
            saea.Dispose();  // TODO: Recycle
        }

        private static void ProcessConnect(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Connect);

            GWTcpSocket gwSocket = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwSocket.Type == GWSocketType.TcpClientConnector);

            CompleteConnect(gwSocket, saea, saea.SocketError);
        }

        private static void InternalStartConnect(SocketAsyncEventArgs saea)
        {
            GWTcpSocket gwSocket = (GWTcpSocket)saea.UserToken;

            Debug.Assert(gwSocket.Type == GWSocketType.TcpClientConnector);

            Debug.Assert(gwSocket._connectionStatus == CONNECT_NOT_ATTEMPTED);

            if (!gwSocket.IncreaseActiveOperationCountIfNotClosed()) {

                saea.Dispose();  // TODO: push into pool

                throw new InvalidOperationException("Can't call InternalStartConnect() as Close() has been called.");
            }

            try {
                if (!gwSocket.Socket.ConnectAsync(saea)) {
                    ProcessConnect(saea);
                }
            }
            catch (SocketException ex) {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                CompleteConnect(gwSocket, saea, ex.SocketErrorCode);
            }
            catch {
                saea.Dispose();  // TODO: push into pool
                throw;
            }

        }

        public void Connect()
        {
            Debug.Assert(this.Type == GWSocketType.TcpClientConnector);

            if (Interlocked.CompareExchange(ref this._connectInvoked, 1, 0) != 0) {
                throw new InvalidOperationException("Connect() has been called");
            }

            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();  // TODO: Pool
            saea.Completed += SocketAsyncEventArgs_OnCompleted;
            saea.UserToken = this;

            saea.RemoteEndPoint = this._connectEndPoint;

            InternalStartConnect(saea);
        }

        #endregion


        #region Receive

        private static void CompleteReceive(
            GWTcpSocket gwSocket,
            SocketAsyncEventArgs saea,
            SocketError status)
        {
            Debug.Assert(gwSocket._connectionStatus == CONNECT_SUCCESSFUL ||
                         gwSocket._connectionStatus == CONNECT_CLOSE_PENDING);
            Debug.Assert(gwSocket._activeOperationCount >= 1);

            if (status == SocketError.Success) {
                gwSocket.OnReceive?.Invoke(gwSocket, saea.Buffer, saea.Offset, saea.BytesTransferred);
            }
            else {
                // TODO: filter the case status == OperationAborted?
                gwSocket.OnReceiveError?.Invoke(gwSocket, status);

                // Filter the case when the connection is reset
                if (status == SocketError.ConnectionReset) {
                    gwSocket.Close();
                }
            }

            Interlocked.Decrement(ref gwSocket._activeOperationCount);
            bool closeCalled = gwSocket.CheckClose();
            if (!closeCalled) {
                InternalStartReceive(saea, false);
            }
            else {
                saea.Dispose();  // TODO: Recycle
            }
        }

        private static void ProcessReceive(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Receive);

            GWTcpSocket gwSocket = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnector ||
                         gwSocket.Type == GWSocketType.TcpClientConnector);

            // NOTE: saea.BytesTransfered can be 0 even if saea.SocketError == Success
            CompleteReceive(gwSocket, saea, saea.SocketError);
        }

        private static void InternalStartReceive(SocketAsyncEventArgs saea, bool throwIfClosed)
        {
            GWTcpSocket gwSocket = (GWTcpSocket)saea.UserToken;

            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnector ||
                         gwSocket.Type == GWSocketType.TcpClientConnector);

            Debug.Assert(gwSocket._connectionStatus != CONNECT_NOT_ATTEMPTED);
            Debug.Assert(gwSocket._connectionStatus != CONNECT_FAILED);

            if (!gwSocket.IncreaseActiveOperationCountIfNotClosed()) {

                saea.Dispose();  // TODO: push into pool

                if (throwIfClosed) {
                    throw new InvalidOperationException("Can't call InternalStartReceive() as Close() has been called.");
                }
                return;
            }

            try {
                if (!gwSocket.Socket.ReceiveAsync(saea)) {
                    ProcessReceive(saea);
                }
            }
            catch (SocketException ex) {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                CompleteReceive(gwSocket, saea, ex.SocketErrorCode);
            }
            catch {
                saea.Dispose();  // TODO: push into pool
                throw;
            }

        }

        public void StartReceive()
        {
            Debug.Assert(this.Type == GWSocketType.TcpServerConnector ||
                         this.Type == GWSocketType.TcpClientConnector);

            switch (this._connectionStatus) {
                case CONNECT_NOT_ATTEMPTED:
                    throw new InvalidOperationException("This socket has not been Connect()ed");
                case CONNECT_FAILED:
                    throw new InvalidOperationException("This socket has failed in Connect()");
            }

            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();  // TODO: Pool
            saea.SetBuffer(new byte[4096], 0, 4096);
            saea.Completed += SocketAsyncEventArgs_OnCompleted;

            saea.UserToken = this;

            InternalStartReceive(saea, false);  // TODO: return a values
        }

        #endregion


        #region Send


        private static void CompleteSend(
            GWTcpSocket gwSocket,
            SocketAsyncEventArgs saea,
            SocketError status)
        {
            Debug.Assert(gwSocket._connectionStatus == CONNECT_SUCCESSFUL ||
                         gwSocket._connectionStatus == CONNECT_CLOSE_PENDING);
            Debug.Assert(gwSocket._activeOperationCount >= 1);

            if (status == SocketError.Success) {
                gwSocket.OnSend?.Invoke(gwSocket, saea.Buffer, saea.Offset, saea.BytesTransferred);
            }
            else {
                gwSocket.OnSendError?.Invoke(gwSocket, status, saea.Buffer, saea.Offset, saea.BytesTransferred);
            }

            Interlocked.Decrement(ref gwSocket._activeOperationCount);
            bool closeCalled = gwSocket.CheckClose();
            saea.Dispose();  // TODO: Recycle
        }

        private static void ProcessSend(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Send);

            GWTcpSocket gwSocket = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnector ||
                         gwSocket.Type == GWSocketType.TcpClientConnector);

            CompleteSend(gwSocket, saea, saea.SocketError);
        }

        private static void InternalStartSend(SocketAsyncEventArgs saea)
        {
            GWTcpSocket gwSocket = (GWTcpSocket)saea.UserToken;

            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnector ||
                         gwSocket.Type == GWSocketType.TcpClientConnector);

            Debug.Assert(gwSocket._connectionStatus != CONNECT_NOT_ATTEMPTED);
            Debug.Assert(gwSocket._connectionStatus != CONNECT_FAILED);

            if (!gwSocket.IncreaseActiveOperationCountIfNotClosed()) {

                saea.Dispose();  // TODO: push into pool

                throw new InvalidOperationException("Can't call InternalStartSend() as Close() has been called.");
            }

            try {
                if (!gwSocket.Socket.SendAsync(saea)) {
                    ProcessSend(saea);
                }
            }
            catch (SocketException ex) {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                CompleteSend(gwSocket, saea, ex.SocketErrorCode);
            }
            catch {
                saea.Dispose();  // TODO: push into pool
                throw;
            }

        }

        public void Send(byte[] buffer, int offset, int count)
        {
            Debug.Assert(this.Type == GWSocketType.TcpServerConnector ||
                         this.Type == GWSocketType.TcpClientConnector);

            switch (this._connectionStatus) {
                case CONNECT_NOT_ATTEMPTED:
                    throw new InvalidOperationException("This socket has not been Connect()ed");
                case CONNECT_FAILED:
                    throw new InvalidOperationException("This socket has failed in Connect()");
            }


            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();  // TODO: Pool
            saea.Completed += SocketAsyncEventArgs_OnCompleted;
            saea.UserToken = this;

            saea.SetBuffer(buffer, offset, count);

            InternalStartSend(saea);
        }

        #endregion


        #region Disconnect

        private static void CompleteDisconnect(GWTcpSocket gwSocket, SocketAsyncEventArgs saea)
        {
            gwSocket.Socket.Dispose();
            saea.Dispose();
        }

        private static void ProcessDisconnect(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Disconnect);

            GWTcpSocket gwSocket = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnector ||
                         gwSocket.Type == GWSocketType.TcpClientConnector);
            
            CompleteDisconnect(gwSocket, saea);
        }

        private static void InternalStartDisconnect(SocketAsyncEventArgs saea)
        {
            GWTcpSocket gwSocket = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnector ||
                         gwSocket.Type == GWSocketType.TcpClientConnector);

            try {
                if (!gwSocket.Socket.DisconnectAsync(saea)) {
                    ProcessDisconnect(saea);
                }
            }
            catch {
                CompleteDisconnect(gwSocket, saea);
            }

        }

        private void Disconnect()
        {
            Debug.Assert(this.Type == GWSocketType.TcpServerConnector ||
                         this.Type == GWSocketType.TcpClientConnector);

            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();  // TODO: Pool
            saea.Completed += SocketAsyncEventArgs_OnCompleted;
            saea.UserToken = this;

            saea.DisconnectReuseSocket = false;

            InternalStartDisconnect(saea);
        }

        #endregion
        
    }
}
