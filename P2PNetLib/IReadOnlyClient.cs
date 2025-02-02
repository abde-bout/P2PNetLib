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
    public interface IReadOnlyClient : ISerializable
    {
        public string Name { get; }
        public int ID { get; }
        public bool Host { get; }
        public bool Self { get; }
        public IPAddress Address { get; }
        public int TcpPort { get; set; }
        public int UdpPort { get; set; }
        public int GetPort(ProtocolType protocole);
        public IPEndPoint GetIP(ProtocolType protocole);
    }
}
