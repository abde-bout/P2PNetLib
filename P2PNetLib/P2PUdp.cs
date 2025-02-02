using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace P2PNet
{
    /// <summary>
    /// Peer to peer using UDP protocole
    /// </summary>
    public class P2PUdp : IP2PVehicle
    {
        public ProtocolType Protocole => ProtocolType.Udp;

        UdpClient _server;

        public async void Send(IPEndPoint ip, byte[] bytes, Action<Exception> onSendCallback)
        {
            EnsureServerIsActive();

            try
            {
                using UdpClient client = new();
                await client.SendAsync(bytes, bytes.Length, ip);

                onSendCallback?.Invoke(null);
            }
            catch (Exception e)
            {
                onSendCallback?.Invoke(e);
            }
        }

        public void StartServer(int serverPort)
        {
            _server?.Close();

            _server = new UdpClient(serverPort);
            _server.AllowNatTraversal(true);
        }

        public byte[] ReadBytes(out IPEndPoint ip)
        {
            EnsureServerIsActive();

            ip = null;

            if (DataIsAvailable())
            {
                ip = null;
                var bytes = _server.Receive(ref ip);
                return bytes;
            }

            return Array.Empty<byte>();
        }

        public bool DataIsAvailable()
        {
            EnsureServerIsActive();

            return _server.Available > 0; //_server.Client.Poll(0, SelectMode.SelectRead)
        }

        public void Dispose()
        {
            _server?.Dispose();
        }

        void EnsureServerIsActive()
        {
            if (_server == null)
            {
                throw new InvalidOperationException($"{nameof(StartServer)} must be called first.");
            }
        }
    }
}
