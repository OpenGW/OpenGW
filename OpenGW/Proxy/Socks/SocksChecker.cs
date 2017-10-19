using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenGW.Networking;

namespace OpenGW.Proxy.Socks
{
    public unsafe class SocksChecker : AbstractProxyChecker
    {
        private CheckerResult TryCheckSocks5(
            byte* recvBuffer, 
            int length, 
            out AbstractProxyChecker newChecker)
        {
            newChecker = null;
            
            if (length < 2 + (int)recvBuffer[1]) {
                return CheckerResult.Uncertain;
            }
            else if (length > 2 + (int)recvBuffer[1]) {
                return CheckerResult.NotMe;
            }
            
            List<byte> respBytes = new List<byte>(32);
            respBytes.Add(0x05);
            respBytes.Add(0x00);  // NO AUTHENTICATION REQUIRED
            this.ClientGwSocket.Send(respBytes.ToArray());
            
            newChecker = new Socks5CmdChecker(this.ClientGwSocket);
            return CheckerResult.IsMe_InProgress;
        }
        
        public override CheckerResult TryCheck(byte* recvBuffer, int length, out AbstractProxyChecker newChecker)
        {
            Debug.Assert(length > 0);
            
            if (recvBuffer[0] == 0x04) {
                newChecker = null;
                if (length < 2) {
                    return CheckerResult.Uncertain;
                }
                
                if (recvBuffer[1] == 0x01) {
                    return CheckerResult.IsMe_InProgress;
                }
                else if (recvBuffer[1] == 0x02) {
                    return CheckerResult.IsMe_InProgress;
                }
                else {
                    return CheckerResult.NotMe;
                }
            }
            else if (recvBuffer[0] == 0x05) {
                if (length < 2) {
                    newChecker = null;
                    return CheckerResult.Uncertain;
                }

                return this.TryCheckSocks5(recvBuffer, length, out newChecker);
            }
            else {
                newChecker = null;
                return CheckerResult.NotMe;
            }
        }

        public SocksChecker(GWTcpSocket clientGwSocket) : base(clientGwSocket)
        {
        }
    }
}