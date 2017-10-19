using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using OpenGW.Networking;
using OpenGW.Proxy.Socks;

namespace OpenGW.Proxy
{
    public unsafe class ProxyClientConnection : IReuseable<GWTcpSocket>
    {        
        private readonly ProxyServer m_ProxyServer;
        private readonly int m_CheckerCount;
        
        private GWTcpSocket m_AcceptedConnection = null;
        private readonly AbstractProxyChecker[] m_Checkers;
        private readonly CheckerResult[] m_CheckerResults;
        private int m_DistinguishedCheckerIndex = -1;
        
        private readonly List<byte> m_PendingRecvBytes = new List<byte>();
        private AbstractProxyHandler m_Handler = null;
        
        
        public void Reinitialize(GWTcpSocket acceptedConnection)
        {
            Debug.Assert(acceptedConnection.TcpServer == this.m_ProxyServer.ServerGwSocket);
            this.m_AcceptedConnection = acceptedConnection;
            
            Debug.Assert(this.m_DistinguishedCheckerIndex == -1);
            Debug.Assert(this.m_PendingRecvBytes.Count == 0);
            Debug.Assert(this.m_Handler == null);
            
            for (int i = 0; i < this.m_CheckerCount; ++i) {
                Debug.Assert(this.m_Checkers[i] == null);
                Debug.Assert(this.m_CheckerResults[i] == CheckerResult.Uncertain);
                
                // TODO: Initialize this.m_Checkers[i]
                this.m_Checkers[i] = new SocksChecker(acceptedConnection);
            }
            
        }

        public void Recycle()
        {
            for (int i = 0; i < this.m_CheckerCount; ++i) {
                this.m_Checkers[i].Dispose();
                this.m_Checkers[i] = null;
                
                this.m_CheckerResults[i] = CheckerResult.Uncertain;
            }
            
            this.m_AcceptedConnection = null;
            this.m_DistinguishedCheckerIndex = -1;

            this.m_PendingRecvBytes.Clear();
            this.m_Handler = null;
        }

        
        private Bool3 TryCheck(byte* recvBuffer, int length)
        {
            if (this.m_DistinguishedCheckerIndex != -1) {
                ref AbstractProxyChecker refYesChecker = ref this.m_Checkers[this.m_DistinguishedCheckerIndex];
                CheckerResult status = refYesChecker.TryCheck(recvBuffer, length, out AbstractProxyChecker newChecker);
                if (newChecker != null && newChecker != refYesChecker) {
                    refYesChecker.Dispose();
                    refYesChecker = newChecker;
                }
                
                switch (status) {
                    case CheckerResult.IsMe_Done:
                        return Bool3.Yes;
                        
                    case CheckerResult.IsMe_InProgress:
                    case CheckerResult.Uncertain:
                        return Bool3.Unknown;
                        
                    case CheckerResult.IsMe_Failed:
                    case CheckerResult.NotMe:
                        return Bool3.No;
                        
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            Debug.Assert(this.m_DistinguishedCheckerIndex == -1);

            
            bool atLeastOneUncertain = false;
            for (int index = 0; index < this.m_CheckerCount; ++index) {
                ref CheckerResult refCurrResult = ref this.m_CheckerResults[index];
                if (refCurrResult == CheckerResult.NotMe) {
                    continue;
                }
                Debug.Assert(refCurrResult == CheckerResult.Uncertain);
                
                ref AbstractProxyChecker refCurrChecker = ref this.m_Checkers[index];
                refCurrResult = refCurrChecker.TryCheck(
                    recvBuffer, 
                    length,
                    out AbstractProxyChecker newChecker);
                if (newChecker != null && newChecker != refCurrChecker) {
                    refCurrChecker.Dispose();
                    refCurrChecker = newChecker;
                }

                switch (refCurrResult) {
                    case CheckerResult.IsMe_Done:
                        this.m_DistinguishedCheckerIndex = index;
                        return Bool3.Yes;
                        
                    case CheckerResult.IsMe_InProgress:
                        this.m_DistinguishedCheckerIndex = index;
                        return Bool3.Unknown;
                        
                    case CheckerResult.IsMe_Failed:
                        this.m_DistinguishedCheckerIndex = index;
                        return Bool3.No;
                        
                    case CheckerResult.NotMe:
                        break;
                        
                    case CheckerResult.Uncertain:
                        atLeastOneUncertain = true;
                        break;
                        
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return atLeastOneUncertain ? Bool3.Unknown : Bool3.No;
        }

        private Bool3 TryGetHandler(byte* recvBuffer, int length, out AbstractProxyHandler handler, out int unusedBytes)
        {
            Bool3 status = this.TryCheck(recvBuffer, length);
            switch (status) {
                case Bool3.No:
                case Bool3.Unknown:
                    handler = null;
                    unusedBytes = 0;
                    return status;
                    
                case Bool3.Yes:
                    AbstractProxyChecker checker = this.m_Checkers[this.m_DistinguishedCheckerIndex];
                    bool success = checker.InitializeHandler(recvBuffer, length, out handler, out unusedBytes);
                    if (success) {
                        Debug.Assert(handler != null);
                        return Bool3.Yes;
                    }
                    else {
                        Debug.Assert(handler == null);
                        return Bool3.No;
                    }
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void OnReceive(byte[] buffer, int offset, int count)
        {
            if (this.m_Handler != null) {
                fixed (byte* recvBuffer = &buffer[offset]) {
                    this.m_Handler.OnReceive(recvBuffer, count);
                }
                return;
            }
                        
            this.m_PendingRecvBytes.AddRange(new ArraySegment<byte>(buffer, offset, count));
                
            fixed (byte* recvBuffer = &this.m_PendingRecvBytes.ToArray()[0]) {  // TODO: performance
                Bool3 success = this.TryGetHandler(
                    recvBuffer, this.m_PendingRecvBytes.Count,
                    out this.m_Handler, 
                    out int unusedBytes);
                switch (success) {
                    case Bool3.No:
                        Debug.Assert(this.m_Handler == null);
                        this.m_AcceptedConnection.Close();
                        break;
                    case Bool3.Unknown:
                        Debug.Assert(this.m_Handler == null);
                        break;
                    case Bool3.Yes:
                        Debug.Assert(this.m_Handler != null);
                        if (unusedBytes > 0) {
                            this.m_Handler.OnReceive(
                                recvBuffer + this.m_PendingRecvBytes.Count - unusedBytes, 
                                unusedBytes);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

        }
        
        
        public ProxyClientConnection(ProxyServer proxyServer)
        {
            this.m_ProxyServer = proxyServer;
            
            // TODO: Initialize m_CheckerCount
            this.m_CheckerCount = 1;
        }
    }
}