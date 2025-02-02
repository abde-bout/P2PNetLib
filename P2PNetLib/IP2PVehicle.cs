using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PNet
{
    /// <summary>
    /// Interface for a peer to peer network message transport protocol
    /// </summary>
    public interface IP2PVehicle : IDisposable
    {
        public ProtocolType Protocole { get; }
        public void Send(IPEndPoint ip, byte[] bytes, Action<Exception> onSendCallback);
        public void StartServer(int serverPort);
        public byte[] ReadBytes(out IPEndPoint ip);
        public bool DataIsAvailable();
    }
}