
## Setup transparent proxy *(iptables)*

Take Google Public DNS (IPv4 & IPv6) as an example.

````bash
iptables  -t nat -I OUTPUT -p tcp -j REDIRECT --to-ports 10000 -d 8.8.8.8
iptables  -t nat -I OUTPUT -p udp -j REDIRECT --to-ports 10000 -d 8.8.8.8
ip6tables -t nat -I OUTPUT -p tcp -j REDIRECT --to-ports 10000 -d 2001:4860:4860::8888
ip6tables -t nat -I OUTPUT -p udp -j REDIRECT --to-ports 10000 -d 2001:4860:4860::8888
````

## Send TCP/UDP packet in shell
````bash
# UDP
echo "This is my UDP data" > /dev/udp/8.8.8.8/10000
# TCP
echo "This is my TCP data" > /dev/tcp/8.8.8.8/10000
````

## C# code

An **unsuccessful** attempt to get the original destination address and port.

**Dont't know how to solve this...**
- Linux: Use kernel TPROXY
- Windows: **TODO**
- OSX: **TODO**

````csharp
const int IP_PKTINFO = 8;
        
Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);
socket.Bind(new IPEndPoint(IPAddress.Loopback, 10000));
byte[] buffer = new byte[65536];
while (true)
{
    SocketFlags flags = SocketFlags.None;
    EndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
    IPPacketInformation pktInfo;
    
    int count = socket.ReceiveMessageFrom(buffer, 0, buffer.Length, ref flags, ref remoteEp, out pktInfo);
    Console.WriteLine(pktInfo.Address);
    Console.WriteLine(pktInfo.Interface);
    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ({flags}) {remoteEp}: {Encoding.UTF8.GetString(buffer, 0, count)}");
}
````
