using P2PNet.Serialization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace P2PNet
{
    /// <summary>
    /// Represents a communication channel utilizing a specific P2P vehicle for sending and receiving data.
    /// </summary>
    public class Channel : IDisposable
    {
        // <summary>
        /// Gets the protocol type used by the vehicle.
        /// </summary>
        public ProtocolType Protocole => _vehicle.Protocole;
        /// <summary>
        /// Queue for managing outgoing packets to be sent.
        /// </summary>
        public ConcurrentQueue<(IPEndPoint, byte[])> SendQueue => _sendQueue;
        /// <summary>
        /// Queue for storing received packets.
        /// </summary>
        public ConcurrentQueue<(IPEndPoint, byte[])> ReadQueue => _readQueue;
        /// <summary>
        /// Queue for tracking unresponsive endpoints during sending attempts.
        /// </summary>
        public ConcurrentQueue<(IPEndPoint, Exception)> UnRensponsiveIPs => _unRensponsiveIPs;

        ConcurrentQueue<(IPEndPoint, byte[])> _readQueue;
        ConcurrentQueue<(IPEndPoint, byte[])> _sendQueue;
        ConcurrentQueue<(IPEndPoint, Exception)> _unRensponsiveIPs;
        IP2PVehicle _vehicle;

        /// <summary>
        /// Initializes a new instance of the <see cref="Channel"/> class with the specified P2P vehicle and server port.
        /// </summary>
        /// <param name="vehicle">The P2P vehicle to handle communication.</param>
        /// <param name="serverPort">The port used by the server to start communication.</param>
        public Channel(IP2PVehicle vehicle, int serverPort)
        {
            _vehicle = vehicle;
            _vehicle.StartServer(serverPort);
            _sendQueue = new();
            _readQueue = new();
            _unRensponsiveIPs = new();
        }

        /// <summary>
        /// Handles broadcasting by processing read and send queues for the channel.
        /// </summary>
        /// <remarks>Not thread safe.</remarks>
        public void Broadcast()
        {
            //read
            ProcessRead();

            //send
            ProcessSend();
        }

        void ProcessRead()
        {
            while (_vehicle.DataIsAvailable())
            {
                var bytes = _vehicle.ReadBytes(out var ipEndPoint);
                if (bytes.Length > 0)
                {
                    _readQueue.Enqueue((ipEndPoint, bytes));
                }
            }
        }

        void ProcessSend()
        {
            while (_sendQueue.TryDequeue(out (IPEndPoint ip, byte[] bytes) packet))
            {
                _vehicle.Send(packet.ip, packet.bytes, (e) =>
                {
                    if (e != null)
                    {
                        _unRensponsiveIPs.Enqueue((packet.ip, e));
                    }
                });
            }
        }

        /// <summary>
        /// Disposes resources used by the channel.
        /// </summary>
        public void Dispose()
        {
            _vehicle?.Dispose();
            _readQueue = null;
            _sendQueue = null;
            _unRensponsiveIPs = null;
        }
    }
}