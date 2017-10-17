using System;
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
        //public delegate void OnConnectDelegate(GWTcpSocket gwConnectedSocket);
        //public delegate void OnConnectErrorDelegate(GWTcpSocket gwSocket, SocketError error);
        public delegate void OnReceiveDelegate(GWTcpSocket gwSocket, byte[] buffer, int offset, int count);
        public delegate void OnReceiveErrorDelegate(GWTcpSocket gwSocket, SocketError error);
        public delegate void OnSendDelegate(GWTcpSocket gwSocket, byte[] buffer, int offset, int count);
        public delegate void OnSendErrorDelegate(GWTcpSocket gwSocket, SocketError error, byte[] buffer, int offset, int count);
        public delegate void OnCloseListenerDelegate(GWTcpSocket gwSocket);
        public delegate void OnCloseConnectionDelegate(GWTcpSocket gwSocket);

        public OnAcceptDelegate OnAccept;
        public OnAcceptErrorDelegate OnAcceptError;
        //public OnConnectDelegate OnConnect;
        //public OnConnectErrorDelegate OnConnectError;
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

        //
        // For TcpClientConnector / TcpServerConnector
        //
        private const int CONNECT_NOT_ATTEMPTED = 0;
        private const int CONNECT_FAILED = 1;
        private const int CONNECT_SUCCESSFUL = 2;
        private const int CONNECT_CLOSE_PENDING = 3;
        private const int CONNECT_CLOSED = 4;
        private volatile int _connectionStatus = CONNECT_NOT_ATTEMPTED;

        private const int INVALID_ACTIVE_OPERATION_COUNT = -1;  // any value < 0
        private int _activeOperationCount = 0;

        private bool IncreaseActiveOperationCountIfNotClosed()
        {
            while (true) {

                if (this._connectionStatus == CONNECT_CLOSE_PENDING ||
                    this._connectionStatus == CONNECT_CLOSED) {
                    return false;
                }

                int oldValue = Volatile.Read(ref this._activeOperationCount);
                if (oldValue == INVALID_ACTIVE_OPERATION_COUNT) return false;
                Debug.Assert(oldValue >= 0);

                if (oldValue == Interlocked.CompareExchange(
                        ref this._activeOperationCount,
                        oldValue + 1,
                        oldValue)) {
                    return true;
                }
            }
        }

        private bool CheckCloseConnection()
        {
            if (this._connectionStatus == CONNECT_CLOSE_PENDING) {
                if (this._activeOperationCount == 0) {
                    int oldValue = Interlocked.CompareExchange(
                        ref this._activeOperationCount,
                        INVALID_ACTIVE_OPERATION_COUNT,
                        0);
                    Debug.Assert(oldValue != INVALID_ACTIVE_OPERATION_COUNT);

                    if (oldValue == 0) {
                        this._connectionStatus = CONNECT_CLOSED;

                        this.OnCloseConnection?.Invoke(this);
                    }
                }
                return true;
            }
            else {
                // Now: this.m_CloseCalled == CLOSE_NOT_CALLED
                return false;
            }
        }


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

            this._connectionStatus = CONNECT_SUCCESSFUL;
        }

        private static void CloseConnector(GWTcpSocket gwSocket)
        {
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerConnector ||
                         gwSocket.Type == GWSocketType.TcpClientConnector);

#pragma warning disable 420
            int oldValue = Interlocked.CompareExchange(
                ref gwSocket._connectionStatus,
                CONNECT_CLOSE_PENDING,
                CONNECT_SUCCESSFUL);
#pragma warning restore 420

            switch (oldValue) {
                case CONNECT_NOT_ATTEMPTED:
                    Console.WriteLine("No Connect(), calling Close() does nothing");
                    return;
                case CONNECT_FAILED:
                    return;
                case CONNECT_CLOSE_PENDING:
                case CONNECT_CLOSED:
                    // TODO: Log
                    Console.WriteLine("Close() again.");
                    return;
                case CONNECT_SUCCESSFUL:
                    try {
                        gwSocket.Socket.Disconnect(false);
                    }
                    catch (Exception ex) {
                        Console.WriteLine(ex);
                        gwSocket.Socket.Dispose();
                    }
                    break;
            }

            bool callClosed = gwSocket.CheckCloseConnection();
            Debug.Assert(callClosed);
        }


        private static void CloseListener(GWTcpSocket gwSocket)
        {
            Debug.Assert(gwSocket.Type == GWSocketType.TcpServerListener);

            try {
                gwSocket.Socket.Close();
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                gwSocket.Socket.Dispose();
            }

            //bool callClosed = gwSocket.CheckCloseConnection();
            //Debug.Assert(callClosed);
        }


        public void Close()
        {
            switch (this.Type) {
                case GWSocketType.TcpServerListener:
                    CloseListener(this);
                    break;
                case GWSocketType.TcpServerConnector:
                case GWSocketType.TcpClientConnector:
                    CloseConnector(this);
                    break;
                case GWSocketType.UdpClient:
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
                case SocketAsyncOperation.Connect:
                case SocketAsyncOperation.Disconnect:
                case SocketAsyncOperation.None:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #region Accept

        private static void InvokeOnAccept(GWTcpSocket gwListener, GWTcpSocket gwAcceptedSocket)
        {
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
            bool closeCalled = gwAcceptedSocket.CheckCloseConnection();
        }


        private static void ProcessAccept(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.Accept);

            GWTcpSocket gwListener = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwListener.Type == GWSocketType.TcpServerListener);

            if (saea.SocketError == SocketError.Success) {
                // Now we successfully accepted a new socket
                Debug.Assert(saea.AcceptSocket != null);
                GWTcpSocket gwSocket = new GWTcpSocket(saea.AcceptSocket, gwListener);

                // Invoke OnAccept
                GWTcpSocket.InvokeOnAccept(gwListener, gwSocket);

                // Accept the next socket
                GWTcpSocket.InternalStartAccept(saea);
            }
            else if (saea.SocketError == SocketError.OperationAborted) {
                // TODO: ?
                gwListener.OnCloseListener?.Invoke(gwListener);
            }
            else {
                gwListener.OnAcceptError?.Invoke(gwListener, saea.SocketError);

                // Accept the next socket
                GWTcpSocket.InternalStartAccept(saea);
            }

        }


        private static void InternalStartAccept(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea != null);
            Debug.Assert(saea.UserToken != null);

            GWTcpSocket gwListener = (GWTcpSocket)saea.UserToken;
            Debug.Assert(gwListener.Type == GWSocketType.TcpServerListener);

            saea.AcceptSocket = null;

            try {
                if (!gwListener.Socket.AcceptAsync(saea)) {
                    GWTcpSocket.ProcessAccept(saea);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted) {
                // Invoke OnCloseListener()
                gwListener.OnCloseListener?.Invoke(gwListener);
            }
            catch (SocketException ex) {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);

                // Invoke OnAcceptError()
                gwListener.OnAcceptError?.Invoke(gwListener, ex.SocketErrorCode);

                // Don't break: just go to accept the next socket!
                GWTcpSocket.InternalStartAccept(saea);
            }
        }


        public void StartAccept()
        {
            Debug.Assert(this.Type == GWSocketType.TcpServerListener);

            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += GWTcpSocket.SocketAsyncEventArgs_OnCompleted;
            saea.UserToken = this;

            GWTcpSocket.InternalStartAccept(saea);
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
            }

            Interlocked.Decrement(ref gwSocket._activeOperationCount);
            bool closeCalled = gwSocket.CheckCloseConnection();
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

            InternalStartReceive(saea, true);
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
            bool closeCalled = gwSocket.CheckCloseConnection();
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
    }
}
