using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenGW.Networking
{
    public partial class GWUdpSocket : GWSocket
    {
        private static readonly GenericPool<SocketAsyncEventArgs> s_SendPool;
        private static readonly GenericPool<SocketAsyncEventArgs> s_ReceiveFromPool;
        private const int RECEIVE_FROM_BUFFER_LENGTH = 65536;
        
        static GWUdpSocket()
        {
            s_ReceiveFromPool = new GenericPool<SocketAsyncEventArgs>
            {
                Generator = () =>
                {
                    SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
                    saea.Completed += GWUdpSocket.SocketAsyncEventArgs_OnCompleted;
                    saea.SetBuffer(new byte[RECEIVE_FROM_BUFFER_LENGTH], 0, RECEIVE_FROM_BUFFER_LENGTH);
                    return saea;
                },
                Cleaner = (SocketAsyncEventArgs saea) =>
                {
                    Debug.Assert(saea.AcceptSocket == null);
                    saea.UserToken = null;
                }
            };
            
            s_SendPool = new GenericPool<SocketAsyncEventArgs>
            {
                Generator = () =>
                {
                    SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
                    saea.Completed += GWUdpSocket.SocketAsyncEventArgs_OnCompleted;
                    return saea;
                },
                Cleaner = (SocketAsyncEventArgs saea) =>
                {
                    Debug.Assert(saea.AcceptSocket == null);
                    saea.SetBuffer(null, 0, 0);
                    saea.UserToken = null;
                }
            };
        }
        
        
        public delegate void OnReceiveFromDelegate(GWUdpSocket gwSocket, IPEndPoint remoteEp, byte[] buffer, int offset, int count);
        public delegate void OnReceiveFromErrorDelegate(GWUdpSocket gwSocket, SocketError error);
        public delegate void OnSendToDelegate(GWUdpSocket gwSocket, IPEndPoint remoteEp, byte[] buffer, int offset, int count);
        public delegate void OnSendToErrorDelegate(GWUdpSocket gwSocket, SocketError error, byte[] buffer, int offset, int count);
        public delegate void OnCloseDelegate(GWUdpSocket gwSocket);

        public OnReceiveFromDelegate OnReceiveFrom;
        public OnReceiveFromErrorDelegate OnReceiveFromError;
        public OnSendToDelegate OnSendTo;
        public OnSendToErrorDelegate OnSendToError;
        public OnCloseDelegate OnClose;

        
        private GWUdpSocket(
            IPEndPoint bindEndPoint, 
            Action<Socket> socketPropertySetter) 
            : base(
                new Socket(bindEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp),
                GWSocketType.UdpClient, 
                socketPropertySetter)
        {
            this.Socket.Bind(bindEndPoint);
            this.LocalEndPoint = (IPEndPoint)this.Socket.LocalEndPoint;

            this._connectionStatus = CONNECT_SUCCESSFUL;
        }

        public static GWUdpSocket CreateUdpServer(
            IPEndPoint bindEndPoint,
            Action<Socket> socketPropertySetter = null)
        {
            return new GWUdpSocket(bindEndPoint, socketPropertySetter);
        }

        public static GWUdpSocket CreateUdpClient(
            AddressFamily bindAddressFamily,
            Action<Socket> socketPropertySetter = null)
        {
            switch (bindAddressFamily) {
                case AddressFamily.InterNetwork:
                    return new GWUdpSocket(new IPEndPoint(IPAddress.Any, 0), socketPropertySetter);
                case AddressFamily.InterNetworkV6:
                    return new GWUdpSocket(new IPEndPoint(IPAddress.IPv6Any, 0), socketPropertySetter);
                default:
                    throw new ArgumentOutOfRangeException(nameof(bindAddressFamily), bindAddressFamily, null);
            }
        }
    }

    public partial class GWUdpSocket
    {

        private static void SocketAsyncEventArgs_OnCompleted(object sender, SocketAsyncEventArgs saea)
        {
            switch (saea.LastOperation) {
                case SocketAsyncOperation.ReceiveFrom:
                    GWUdpSocket.ProcessReceiveFrom(saea);
                    break;
                case SocketAsyncOperation.SendTo:
                    GWUdpSocket.ProcessSendTo(saea);
                    break;
                //case SocketAsyncOperation.Accept:
                //case SocketAsyncOperation.Connect:
                //case SocketAsyncOperation.Disconnect:
                //case SocketAsyncOperation.Receive:
                //case SocketAsyncOperation.Send:
                //case SocketAsyncOperation.ReceiveMessageFrom:
                //case SocketAsyncOperation.SendPackets:
                //case SocketAsyncOperation.None:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override void DoClose(int oldConnectionStatus)
        {
            try
            {
                this.Socket.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.Socket.Dispose();
            }
        }

        protected override void InvokeCloseCallback()
        {
            this.OnClose?.Invoke(this);
        }

        
        #region ReceiveFrom

        private static void CompleteReceiveFrom(
            GWUdpSocket gwSocket,
            SocketAsyncEventArgs saea,
            SocketError status)
        {
            Debug.Assert(gwSocket._connectionStatus == CONNECT_SUCCESSFUL ||
                         gwSocket._connectionStatus == CONNECT_CLOSE_PENDING);
            Debug.Assert(gwSocket._activeOperationCount >= 1);

            if (status == SocketError.Success) {
                gwSocket.OnReceiveFrom?.Invoke(
                    gwSocket, 
                    (IPEndPoint)saea.RemoteEndPoint, 
                    saea.Buffer, saea.Offset, saea.BytesTransferred);
            }
            else {
                // TODO: filter the case status == OperationAborted?
                gwSocket.OnReceiveFromError?.Invoke(gwSocket, status);

                // Filter the case when the connection is reset
                //if (status == SocketError.ConnectionReset) {
                //    gwSocket.Close();
                //}
            }

            bool closeCalled = gwSocket.DecreaseActiveOperationCountAndCheckClose();
            if (!closeCalled) {
                GWUdpSocket.InternalStartReceiveFrom(saea, false);
            }
            else {
                s_ReceiveFromPool.Push(saea);
            }
        }

        private static void ProcessReceiveFrom(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.ReceiveFrom);

            GWUdpSocket gwSocket = (GWUdpSocket)saea.UserToken;
            Debug.Assert(gwSocket.Type == GWSocketType.UdpClient);

            GWUdpSocket.CompleteReceiveFrom(gwSocket, saea, saea.SocketError);
        }

        private static void InternalStartReceiveFrom(SocketAsyncEventArgs saea, bool throwIfClosed)
        {
            GWUdpSocket gwSocket = (GWUdpSocket)saea.UserToken;

            Debug.Assert(gwSocket.Type == GWSocketType.UdpClient);

            Debug.Assert(gwSocket._connectionStatus != CONNECT_NOT_ATTEMPTED);
            Debug.Assert(gwSocket._connectionStatus != CONNECT_FAILED);

            if (!gwSocket.IncreaseActiveOperationCountIfNotClosed()) {

                s_ReceiveFromPool.Push(saea);

                if (throwIfClosed) {
                    throw new InvalidOperationException("Can't call InternalStartReceiveFrom() as Close() has been called.");
                }
                return;
            }

            try {
                saea.RemoteEndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);  // TODO: Cache

                if (!gwSocket.Socket.ReceiveFromAsync(saea)) {
                    GWUdpSocket.ProcessReceiveFrom(saea);
                }
            }
            catch (SocketException ex) {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                GWUdpSocket.CompleteReceiveFrom(gwSocket, saea, ex.SocketErrorCode);
            }
            catch {
                s_ReceiveFromPool.Push(saea);
                throw;
            }

        }

        public void StartReceiveFrom()
        {
            Debug.Assert(this.Type == GWSocketType.UdpClient);

            switch (this._connectionStatus) {
                case CONNECT_NOT_ATTEMPTED:
                    throw new InvalidOperationException("This socket has not been Connect()ed");
                case CONNECT_FAILED:
                    throw new InvalidOperationException("This socket has failed in Connect()");
            }

            SocketAsyncEventArgs saea = s_ReceiveFromPool.Pop();
            saea.UserToken = this;

            GWUdpSocket.InternalStartReceiveFrom(saea, false);  // TODO: return a values
        }

        #endregion


        #region SendTo


        private static void CompleteSendTo(
            GWUdpSocket gwSocket,
            SocketAsyncEventArgs saea,
            SocketError status)
        {
            Debug.Assert(gwSocket._connectionStatus == CONNECT_SUCCESSFUL ||
                         gwSocket._connectionStatus == CONNECT_CLOSE_PENDING);
            Debug.Assert(gwSocket._activeOperationCount >= 1);

            if (status == SocketError.Success) {
                gwSocket.OnSendTo?.Invoke(gwSocket, (IPEndPoint)saea.RemoteEndPoint, saea.Buffer, saea.Offset, saea.BytesTransferred);
            }
            else {
                gwSocket.OnSendToError?.Invoke(gwSocket, status, saea.Buffer, saea.Offset, saea.BytesTransferred);
            }

            gwSocket.DecreaseActiveOperationCountAndCheckClose();
            s_SendPool.Push(saea);
        }

        private static void ProcessSendTo(SocketAsyncEventArgs saea)
        {
            Debug.Assert(saea.LastOperation == SocketAsyncOperation.SendTo);

            GWUdpSocket gwSocket = (GWUdpSocket)saea.UserToken;
            Debug.Assert(gwSocket.Type == GWSocketType.UdpClient);

            GWUdpSocket.CompleteSendTo(gwSocket, saea, saea.SocketError);
        }

        private static void InternalStartSendTo(SocketAsyncEventArgs saea)
        {
            GWUdpSocket gwSocket = (GWUdpSocket)saea.UserToken;

            Debug.Assert(gwSocket.Type == GWSocketType.UdpClient);

            Debug.Assert(gwSocket._connectionStatus != CONNECT_NOT_ATTEMPTED);
            Debug.Assert(gwSocket._connectionStatus != CONNECT_FAILED);

            if (!gwSocket.IncreaseActiveOperationCountIfNotClosed()) {

                s_SendPool.Push(saea);

                throw new InvalidOperationException("Can't call InternalStartSendTo() as Close() has been called.");
            }

            try {
                if (!gwSocket.Socket.SendToAsync(saea)) {
                    GWUdpSocket.ProcessSendTo(saea);
                }
            }
            catch (SocketException ex) {
                Debug.Assert(ex.SocketErrorCode != SocketError.Success);
                GWUdpSocket.CompleteSendTo(gwSocket, saea, ex.SocketErrorCode);
            }
            catch {
                s_SendPool.Push(saea);
                throw;
            }

        }

        public void SendTo(IPEndPoint remoteEp, byte[] buffer, int offset, int count)
        {
            Debug.Assert(this.Type == GWSocketType.UdpClient);

            switch (this._connectionStatus) {
                case CONNECT_NOT_ATTEMPTED:
                    throw new InvalidOperationException("This socket has not been Connect()ed");
                case CONNECT_FAILED:
                    throw new InvalidOperationException("This socket has failed in Connect()");
            }


            SocketAsyncEventArgs saea = s_SendPool.Pop();
            saea.UserToken = this;
            saea.RemoteEndPoint = remoteEp;
            saea.SetBuffer(buffer, offset, count);

            GWUdpSocket.InternalStartSendTo(saea);
        }

        #endregion

    }
}