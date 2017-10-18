using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OpenGW.Networking
{
    public abstract class GWSocket
    {
        protected Socket Socket { get; }

        public IPEndPoint LocalEndPoint { get; protected set; }

        public IPEndPoint RemoteEndPoint { get; protected set; }


        protected GWSocketType Type { get; }

        protected Action<Socket> SocketPropertySetter { get; }

        
        //
        // For all: TcpClientConnector, TcpServerConnector, TcpServerListener
        //
        protected const int CONNECT_NOT_ATTEMPTED = 0;
        protected const int CONNECT_FAILED = 1;
        protected const int CONNECT_SUCCESSFUL = 2;
        protected const int CONNECT_CLOSE_PENDING = 3;
        protected const int CONNECT_CLOSED = 4;
        protected volatile int _connectionStatus = CONNECT_NOT_ATTEMPTED;

        protected const int INVALID_ACTIVE_OPERATION_COUNT = -1;  // any value < 0
        protected int _activeOperationCount = 0;

        protected int _closeInvoked = 0;  // Whether this.Close() is called
        
       
        protected bool IncreaseActiveOperationCountIfNotClosed()
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

        protected bool DecreaseActiveOperationCountAndCheckClose()
        {
            Interlocked.Decrement(ref this._activeOperationCount);
            return this.CheckClose();
        }

        protected abstract void InvokeCloseCallback();
        
        private bool CheckClose()
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

                        this.InvokeCloseCallback();
                    }
                }
                return true;
            }
            else if (this._connectionStatus == CONNECT_CLOSED) {
                return true;
            }
            else {
                // Now: this.m_CloseCalled == CLOSE_NOT_CALLED
                return false;
            }
            
        }

        
        protected abstract void DoClose(int oldConnectionStatus);

        public void Close()
        {
            if (Interlocked.CompareExchange(ref this._closeInvoked, 1, 0) != 0) {
                // TODO: Log warning: Close() again
                return;
            }

            Debug.Assert(this._connectionStatus != CONNECT_CLOSE_PENDING);
            Debug.Assert(this._connectionStatus != CONNECT_CLOSED);

//#pragma warning disable 420
//            int oldValue = Interlocked.CompareExchange(
//                ref this._connectionStatus,
//                CONNECT_CLOSE_PENDING,
//                CONNECT_SUCCESSFUL);
//#pragma warning restore 420
#pragma warning disable 420
            int oldValue = Interlocked.Exchange(
                ref this._connectionStatus,
                CONNECT_CLOSE_PENDING);
#pragma warning restore 420
            Debug.Assert(oldValue != CONNECT_CLOSE_PENDING);
            Debug.Assert(oldValue != CONNECT_CLOSED);

            this.DoClose(oldValue);
            
            bool callClosed = this.CheckClose();
            Debug.Assert(callClosed);
        }

        
        
        
        protected GWSocket(Socket socket, GWSocketType type, Action<Socket> socketPropertySetter)
        {
            this.Socket = socket;
            this.Type = type;
            this.SocketPropertySetter = socketPropertySetter;

            switch (this.Type) {
                case GWSocketType.TcpServerListener:
                case GWSocketType.TcpServerConnector:
                case GWSocketType.TcpClientConnector:
                    Debug.Assert(socket.ProtocolType == ProtocolType.Tcp);
                    Debug.Assert(socket.SocketType == SocketType.Stream);
                    break;
                case GWSocketType.UdpClient:
                    Debug.Assert(socket.ProtocolType == ProtocolType.Udp);
                    Debug.Assert(socket.SocketType == SocketType.Dgram);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            this.SocketPropertySetter?.Invoke(socket);
        }

        public override string ToString()
        {
            switch (this.Type) {
                case GWSocketType.TcpServerListener:
                    return $"(S@ {this.LocalEndPoint})";
                case GWSocketType.TcpServerConnector:
                    return $"(S> {this.LocalEndPoint}->{this.RemoteEndPoint})";
                case GWSocketType.TcpClientConnector:
                    return $"(C> {this.LocalEndPoint?.ToString() ?? "<null>"}->{this.RemoteEndPoint?.ToString() ?? "<null>"})";
                case GWSocketType.UdpClient:
                    return $"(U@ {this.LocalEndPoint})";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
