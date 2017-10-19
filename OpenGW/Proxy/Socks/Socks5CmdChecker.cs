﻿using System;
using OpenGW.Networking;

namespace OpenGW.Proxy.Socks
{
    public unsafe class Socks5CmdChecker : AbstractProxyChecker
    {
        public Socks5CmdChecker(GWTcpSocket clientGwSocket) : base(clientGwSocket)
        {
        }

        public override CheckerResult TryCheck(byte* recvBuffer, int length, out AbstractProxyChecker newChecker)
        {
            /*
                +----+-----+-------+------+----------+----------+
                |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
                +----+-----+-------+------+----------+----------+
                | 1  |  1  | X'00' |  1   | Variable |    2     |
                +----+-----+-------+------+----------+----------+
        
             Where:
        
                  o  VER    protocol version: X'05'
                  o  CMD
                     o  CONNECT X'01'
                     o  BIND X'02'
                     o  UDP ASSOCIATE X'03'
                  o  RSV    RESERVED
                  o  ATYP   address type of following address
                     o  IP V4 address: X'01'
                     o  DOMAINNAME: X'03'
                     o  IP V6 address: X'04'
                  o  DST.ADDR       desired destination address
                  o  DST.PORT desired destination port in network octet
                     order


             In an address field (DST.ADDR, BND.ADDR), the ATYP field specifies
             the type of address contained within the field:
               
              o  X'01'
               the address is a version-4 IP address, with a length of 4 octets
        
              o  X'03'
               the address field contains a fully-qualified domain name.  The first
               octet of the address field contains the number of octets of name that
               follow, there is no terminating NUL octet.
        
              o  X'04'
               the address is a version-6 IP address, with a length of 16 octets.
            */
            throw new NotImplementedException();
        }

    }
}