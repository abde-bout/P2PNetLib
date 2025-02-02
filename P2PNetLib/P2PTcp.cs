using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace P2PNet
{
    /// <summary>
    /// Peer to peer using TCP protocole
    /// </summary>
    public class P2PTcp : IP2PVehicle
    {
        const int TCP_BUFFER_SIZE = 1024;

        public ProtocolType Protocole => ProtocolType.Tcp;

        Dictionary<IPEndPoint, TcpClient> _sendClients;
        Dictionary<IPEndPoint, TcpClient> _readClients;
        TcpListener _server;
        Tcpt _tcpt;
        Queue<(IPEndPoint, byte[])> _readBuffers;
        Queue<IPEndPoint> _forgetReadClients;

        public async void Send(IPEndPoint ip, byte[] bytes, Action<Exception> onSendCallback)
        {
            EnsureServerIsActive();

            TcpClient client = null;
            Exception e = null;

            try
            {
                if (!_sendClients.TryGetValue(ip, out client) || !client.Connected)
                {
                    client = new TcpClient();
                    client.NoDelay = true;
                    client.Connect(ip.Address, ip.Port);
                    _sendClients[ip] = client;
                }

                if (client.Connected)
                {
                    //Console.WriteLine("sent_bytes: " + bytes.Length);

                    if (Tcpt.FinalBufferSize(bytes.Length) > TCP_BUFFER_SIZE)
                    {
                        throw new InvalidOperationException($"Tcpt packets cannot exceed {TCP_BUFFER_SIZE} bytes.");
                    }

                    byte[] buffer = _tcpt.CreateTcpt(bytes);
                    using var stream = client.GetStream();
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                }
                else
                {
                    //failed/lost connection
                    throw new InvalidOperationException("Connection was lost or could not be made with the target ip.");
                }
            }
            catch (Exception ex)
            {
                //failed send
                e = ex;

                //close client
                client?.Close();
                _sendClients.Remove(ip);
            }
            finally
            {
                onSendCallback?.Invoke(e);
            }
        }

        public void StartServer(int serverPort)
        {
            _server?.Stop();

            _tcpt = new();
            _readBuffers = new();
            _readClients = new();
            _sendClients = new();
            _forgetReadClients = new();

            _server = new(IPAddress.Any, serverPort);
            _server.AllowNatTraversal(true);
            _server.Start();
        }

        public byte[] ReadBytes(out IPEndPoint ipEndPoint)
        {
            EnsureServerIsActive();

            //check for pending
            if (_server.Pending())
            {
                var incommingClient = _server.AcceptTcpClient();
                _readClients.Add((IPEndPoint)incommingClient.Client.RemoteEndPoint, incommingClient);
            }

            //evaluate connected clients
            foreach (var client in _readClients)
            {
                if (client.Value.Connected)
                {
                    using var stream = client.Value.GetStream();
                    if (stream.DataAvailable)
                    {
                        var buffer = new byte[TCP_BUFFER_SIZE];

                        int bytesRead = stream.Read(buffer, 0, buffer.Length);

                        if (bytesRead == 0)
                        {
                            buffer = Array.Empty<byte>();
                        }
                        else
                        {
                            buffer = _tcpt.ReadTcpt(buffer, 0, bytesRead);
                        }

                        _readBuffers.Enqueue(((IPEndPoint)client.Value.Client.RemoteEndPoint, buffer));
                        //Console.WriteLine("read_bytes: " + buffer.Length);
                    }
                }
                else
                {
                    _forgetReadClients.Enqueue(client.Key);
                }
            }

            //clear disconnected clients
            while (_forgetReadClients.Count > 0)
            {
                var forgetClient = _forgetReadClients.Dequeue();
                _readClients[forgetClient].Close();
                _readClients.Remove(forgetClient);
            }

            if (_readBuffers.Count <= 0)
            {
                ipEndPoint = null;
                return Array.Empty<byte>();
            }

            (IPEndPoint ip, byte[] bytes) readBuffer = _readBuffers.Dequeue();
            ipEndPoint = readBuffer.ip;
            return readBuffer.bytes;
        }

        public bool DataIsAvailable()
        {
            EnsureServerIsActive();

            if (_readBuffers.Count > 0) return true;

            if (_server.Pending()) return true;

            foreach (var client in _readClients.Values)
            {
                if (client.Connected && client.GetStream().DataAvailable) return true;
            }

            return false;
        }

        public void Dispose()
        {
            _server?.Stop();
            _tcpt?.Dispose();
            _readBuffers?.Clear(); _readBuffers = null;
            _readClients?.Clear(); _readClients = null;
            _sendClients?.Clear(); _sendClients = null;
            _forgetReadClients?.Clear(); _forgetReadClients = null;
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
