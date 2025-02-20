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
        TcptReader _tcptReader;
        Queue<(IPEndPoint, byte[])> _readBuffers;
        Queue<IPEndPoint> _forgetReadClients;

        public void Send(IPEndPoint ip, byte[] bytes, Action<Exception> onSendCallback)
        {
            EnsureServerIsActive();

            TcpClient client = null;
            Exception e = null;

            try
            {
                bool clientNotAvailable = !_sendClients.TryGetValue(ip, out client) || !client.Connected;

                if (clientNotAvailable)
                {
                    client?.Close();

                    client = new() { NoDelay = true };

                    _sendClients[ip] = client;

                    client.Connect(ip);
                }

                if (client == null) throw new AccessViolationException("client is null...%%%");

                if (client.Connected)
                {
                    if (TcptReader.FinalBufferSize(bytes.Length) > TCP_BUFFER_SIZE)
                    {
                        throw new InvalidOperationException($"Tcpt packets cannot exceed {TCP_BUFFER_SIZE} bytes.");
                    }

                    byte[] buffer = TcptWriter.CreateTcptPacket(bytes);
                    NetworkStream stream = client.GetStream();
                    stream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    //failed/lost connection
                    throw new InvalidOperationException("Connection was lost or could not be made with the target ip.");//
                }
            }
            catch (Exception ex)
            {
                //failed send
                e = ex;
            }
            finally
            {
                onSendCallback?.Invoke(e);
            }
        }

        public void StartServer(int serverPort)
        {
            _server?.Stop();

            _tcptReader = new();
            _readBuffers = new();
            _readClients = new();
            _sendClients = new();
            _forgetReadClients = new();

            _server = new(IPAddress.Any, serverPort);
            //_server.AllowNatTraversal(true);

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
                    var stream = client.Value.GetStream();
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
                            foreach (var payload in _tcptReader.ReadTcptPackets(buffer, 0, bytesRead))
                            {
                                _readBuffers.Enqueue(((IPEndPoint)client.Value.Client.RemoteEndPoint, payload));
                            }
                        }
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
            if (_sendClients != null && _sendClients.Count > 0)
            {
                foreach (var client in _sendClients.Values)
                {
                    client?.Close();
                }
            }

            if (_readClients != null && _readClients.Count > 0)
            {
                foreach (var client in _readClients.Values)
                {
                    client?.Close();
                }
            }

            _server?.Stop();
            _tcptReader?.Dispose();
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
