using P2PNet.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace P2PNet
{
    public class Client : IReadOnlyClient
    {
        public IPEndPoint TcpIP => new IPEndPoint(Address, TcpPort);
        public IPEndPoint UdpIP => new IPEndPoint(Address, UdpPort);
        public string Name { get; set; }
        public int ID { get; set; }
        public bool Host { get; set; }
        public bool Self { get; set; }
        public IPAddress Address { get; set; }
        public int TcpPort { get; set; }
        public int UdpPort { get; set; }

        public IPEndPoint GetIP(ProtocolType protocole)
        {
            return new IPEndPoint(Address, GetPort(protocole));
        }

        public int GetPort(ProtocolType protocole)
        {
            switch (protocole)
            {
                case ProtocolType.Udp:
                    return UdpPort;
                case ProtocolType.Tcp:
                default:
                    return TcpPort;
            }
        }

        public object Deserialize(Serializer serializer)
        {
            return new Client()
            {
                Name = serializer.GetNextItem<string>(),
                ID = serializer.GetNextItem<int>(),
                Host = serializer.GetNextItem<bool>(),
                Self = serializer.GetNextItem<bool>(),
                Address = new IPAddress(serializer.GetNextItem<byte[]>()),
                TcpPort = serializer.GetNextItem<int>(),
                UdpPort = serializer.GetNextItem<int>(),
            };
        }

        public byte[] Serialize(object obj)
        {
            return Serializer.SerializeItems(((Client)obj).Name,
                ((Client)obj).ID,
                ((Client)obj).Host,
                ((Client)obj).Self,
                ((Client)obj).Address.GetAddressBytes(),
                ((Client)obj).TcpPort,
                ((Client)obj).UdpPort);
        }
    }
}
