using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using P2PNet.Serialization;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Threading;
using P2PNet.Cryptography;
using System.Security.Cryptography;

namespace P2PNet
{
    /// <summary>
    /// Peer-to-peer communication service between multiple clients.
    /// </summary>
    public class P2PService
    {
        //consts
        const int BROADCAST_DELAY = 10; //avoid busy looping
        const string SERVER_KEY = "fv89n4-356jb3-lsp894";

        //codes
        public readonly static Code JOIN_REQUEST_CODE = Code.Register(0, ProtocolType.Tcp);
        public readonly static Code JOIN_ANSWER_CODE = Code.Register(1, ProtocolType.Tcp);
        public readonly static Code ADD_CLIENT_CODE = Code.Register(2, ProtocolType.Tcp);
        public readonly static Code REMOVE_CLIENT_CODE = Code.Register(3, ProtocolType.Tcp);
        public readonly static Code DATA_CODE = Code.Register(4, ProtocolType.Tcp);

        //callbacks
        /// <summary>
        /// Event triggered when a packet is received.
        /// </summary>
        public event Action<Packet> OnPacketReception;
        /// <summary>
        /// Event triggered when a client connects to the server.
        /// </summary>
        public event Action<IReadOnlyClient> OnClientConnected;
        /// <summary>
        /// Event triggered when a client disconnects from the server, with details.
        /// </summary>
        public event Action<IReadOnlyClient, DisconnectInfo> OnClientDisconnected;
        /// <summary>
        /// Event triggered when a join request receives a response.
        /// </summary>
        public event Action<JoinRequestAnswerInfo> OnJoinRequestAnswer;
        /// <summary>
        /// Event triggered when a message fails to send to a target endpoint.
        /// </summary>
        public event Action<(IPEndPoint, Exception)> OnFailToSend;

        //public
        /// <summary>
        /// Current service status based on server and connection state.
        /// </summary>
        public ServiceStatus Status => ConnectedToServer ? ServiceStatus.Connected : ServerIsActive ? ServiceStatus.Online : ServiceStatus.Offline;
        /// <summary>
        /// Indicates whether this client is connected to a host server.
        /// </summary>
        public bool ConnectedToServer => _connectedToServer; //this client is connected to host server
        /// <summary>
        /// Indicates whether the server is active and ready for read/send operations.
        /// </summary>
        public bool ServerIsActive => _serverIsActive; //the read/send server is active
        /// <summary>
        /// Maximum number of clients allowed to connect to the server.
        /// </summary>
        public int MaxClient => _maxClient;
        /// <summary>
        /// Represents the current client's information.
        /// </summary>
        public IReadOnlyClient SelfClient => _clients[_selfClientID];
        /// <summary>
        /// Provides a collection of all connected clients.
        /// </summary>
        public IReadOnlyCollection<IReadOnlyClient> Clients => _clients.Values;

        //Channels
        Channel _tcpChannel;
        Channel _udpChannel;

        //Comm
        CancellationTokenSource _threadToken;
        Thread _mainThread;

        //cache
        int _selfClientID;
        bool _serverIsActive;
        int _maxClient;
        bool _connectedToServer;
        string _password;


        Dictionary<int, Client> _clients;
        Dictionary<int, Action<Packet>> _codeCallbackMap;
        /// <summary>
        /// Service constructor.
        /// </summary>
        public P2PService()
        {
        }

        void EnsureServerIsActive()
        {
            if (!_serverIsActive)
                throw new InvalidOperationException("Server is not active.");
        }

        void StartServer(string name, bool host)
        {
            Stop();

            _serverIsActive = true;
            _connectedToServer = host;
            _selfClientID = 0;

            Client selfClient = new()
            {
                ID = _selfClientID,
                Name = CheckClientName(name, _selfClientID, host),
                Address = GetLocalIPAddress(),
                TcpPort = GetAvailablePort(),
                UdpPort = GetAvailablePort(),
                Self = true,
                Host = host,
            };

            _clients = new()
            {
                { selfClient.ID, selfClient }
            };

            _codeCallbackMap = new()
            {
                { JOIN_REQUEST_CODE, OnJoinRequest },
                { JOIN_ANSWER_CODE, OnJoinAnswer },
                { ADD_CLIENT_CODE, OnAddClient },
                { REMOVE_CLIENT_CODE, OnRemoveClient },
            };

            //channels
            _tcpChannel = new(new P2PTcp(), selfClient.TcpPort);
            _udpChannel = new(new P2PUdp(), selfClient.UdpPort);

            //internal thread
            _threadToken = new();
            _mainThread = new Thread(() => MainThread(_threadToken.Token)) { IsBackground = true };
            _mainThread.Start();
        }
        /// <summary>  Hosts a server with a host name and maximum number of clients. </summary>
        public void HostServer(string name, int maxClient)
        {
            StartServer(name, true);

            _maxClient = maxClient;
            _password = P2PEncryption.GenerateRandomKey(8);
        }
        /// <summary> Connects to a server with a name and an invite code. </summary>
        public void ConnectToServer(string name, string code)
        {
            StartServer(name, false);

            (IPEndPoint ip, string password) server = ParseServerCode(code);

            Send(JOIN_REQUEST_CODE, server.ip, new object[]
            {
                server.password,
                name, SelfClient.TcpPort,
                SelfClient.UdpPort,
                SelfClient.Address.GetAddressBytes()
            });
        }

        /// <summary>
        /// Sends args to clients based on the specified filter.
        /// </summary>
        public void Send(Code code, Filter filter, params object[] args)
        {
            foreach (var client in _clients.Values)
            {
                switch (filter)
                {
                    case Filter.AllExceptSelf:
                        if (client.Self) continue;
                        break;

                    case Filter.Host:
                        if (!client.Host) continue;
                        break;

                    case Filter.All:
                    default:
                        break;
                }

                Send(code, client.ID, args);
            }
        }
        /// <summary>
        /// Sends args to a specified client.
        /// </summary>
        public void Send(Code code, IReadOnlyClient client, params object[] args)
        {
            Send(code, client.ID, args);
        }
        /// <summary>
        /// Sends args to a specified client id.
        /// </summary>
        public void Send(Code code, int clientID, params object[] args)
        {
            Send(code,
                _clients[clientID].GetIP(code.Protocole),
                args);
        }
        /// <summary>
        /// Sends args to a specified ip.
        /// </summary>
        public void Send(Code code, IPEndPoint targetIP, params object[] args)
        {
            EnsureServerIsActive();

            //create packet
            Packet packet = new(code, SelfClient.ID, Serializer.SerializeItems(args));

            //serialize packet
            var paquetBytes = Serializer.SerializeItems(packet);

            var channel = code.Protocole == ProtocolType.Tcp ? _tcpChannel : _udpChannel;

            //enqueue packet
            channel.SendQueue.Enqueue((targetIP, paquetBytes));
        }

        /// <summary> Generates and returns the server's invite code.</summary>
        public string GetServerCode()
        {
            EnsureServerIsActive();

            if (!SelfClient.Host) return string.Empty;

            return P2PEncryption.Encrypt($"{SelfClient.Address}${SelfClient.TcpPort}${_password}", SERVER_KEY);
        }
        (IPEndPoint, string) ParseServerCode(string code)
        {
            var decryptedText = P2PEncryption.Decrypt(code, SERVER_KEY);
            var splits = decryptedText.Split('$');
            var address = splits[0];
            var port = int.Parse(splits[1]);
            var ip = new IPEndPoint(IPAddress.Parse(address), port);
            var password = splits[2];
            return (ip, password);
        }

        /// <summary>
        /// Retrieves the client who sent the given packet.
        /// </summary>
        public IReadOnlyClient GetSenderClient(Packet paquet) => GetClient(paquet.ID);
        /// <summary>
        /// Retrieves a client by their unique client ID.
        /// </summary>
        public IReadOnlyClient GetClient(int clientID) => _clients[clientID];
        /// <summary>
        /// Retrieves a client matching the specified IP endpoint.
        /// </summary>
        public IReadOnlyClient GetClient(IPEndPoint clientIp)
        {
            return GetClient(client =>
            {
                return client.Address.Equals(clientIp.Address) &&
                (client.TcpPort == clientIp.Port ||
                client.UdpPort == clientIp.Port);
            });
        }
        /// <summary>
        /// Retrieves the first client that matches the given predicate.
        /// </summary>
        public IReadOnlyClient GetClient(Predicate<IReadOnlyClient> predicate)
        {
            return _clients.Values.Where(client => predicate(client)).FirstOrDefault();
        }

        int NextClientID()
        {
            for (int i = 0; i < _clients.Count; i++)
            {
                if (!_clients.ContainsKey(i)) return i;
            }

            return _clients.Count;
        }
        string CheckClientName(string name, int id, bool host)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = host ? "Host" : "Client";
            }

            return _clients != null && GetClient(c => c.Name.Equals(name)) != null ? $"{name}_{id}" : name;
        }

        void MainThread(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_serverIsActive) Broadcast(_tcpChannel);
                if (_serverIsActive) Broadcast(_udpChannel);

                Thread.Sleep(BROADCAST_DELAY);
            }
        }
        void Broadcast(Channel channel)
        {
            channel.Broadcast(); //tick channel

            while (channel.UnRensponsiveIPs.TryDequeue(out (IPEndPoint ip, Exception e) response))
            {
                var client = GetClient(response.ip);

                if (ConnectedToServer && client != null)
                {
                    if (SelfClient.Host)
                    {
                        DisconnectClientAsHost(client.ID, DisconnectInfo.UnResponsive);
                    }
                    else
                    {
                        //host not answering
                        var selfClient = SelfClient;
                        Stop();
                        OnClientDisconnected?.Invoke(selfClient, DisconnectInfo.UnResponsive);
                        return;
                    }
                }

                OnFailToSend?.Invoke(response);
            }

            while (channel.ReadQueue.TryDequeue(out (IPEndPoint ip, byte[] bytes) readData))
            {
                OnReadData(readData.bytes);
            }
        }

        void DisconnectClientAsHost(int clientID, DisconnectInfo info)
        {
            var reClient = _clients[clientID];
            _clients.Remove(clientID);
            OnClientDisconnected?.Invoke(reClient, info);
            foreach (var client in _clients.Values)
                if (client.Host) continue;
                else Send(REMOVE_CLIENT_CODE, client, clientID, (byte)info);
        }
        void OnReadData(byte[] bytes)
        {
            var packet = Serializer.DeserializeItem<Packet>(bytes);

            if (_codeCallbackMap.TryGetValue(packet.Code, out var callback))
            {
                callback(packet);
            }

            OnPacketReception?.Invoke(packet);
        }

        /// <summary> Stops the server and cleans up all resources. </summary>
        public void Stop()
        {
            if (!_serverIsActive)
                return;

            _serverIsActive = false;
            _connectedToServer = false;

            _threadToken?.Cancel();
            if (Thread.CurrentThread != _mainThread) _mainThread?.Join();

            _tcpChannel?.Dispose();
            _udpChannel?.Dispose();

            _clients?.Clear(); _clients = null;
            _selfClientID = 0;
            _maxClient = 0;
            _password = null;
            _codeCallbackMap?.Clear();
        }

        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.FirstOrDefault(addr => addr.AddressFamily == AddressFamily.InterNetwork) ?? IPAddress.Any;
        }
        public static int GetAvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                return ((socket.LocalEndPoint as IPEndPoint)?.Port) ?? 0;
            }
        }

        void OnJoinRequest(Packet packet)
        {
            Serializer serializer = new(packet.Bytes);
            var password = serializer.GetNextItem<string>();
            var clientName = serializer.GetNextItem<string>();
            var clientTcpPort = serializer.GetNextItem<int>();
            var clientUdpPort = serializer.GetNextItem<int>();
            var clientAddress = serializer.GetNextItem<byte[]>();

            int clientID = -1;
            int maxClient = -1;
            JoinRequestAnswerInfo info;
            IReadOnlyClient[] syncClients = new IReadOnlyClient[] { };

            if (_password.Equals(password))
            {
                if (_clients.Count + 1 <= _maxClient)
                {
                    info = JoinRequestAnswerInfo.Accepted;

                    maxClient = _maxClient;
                    clientID = NextClientID();

                    //add incomming client to host
                    Client incommingClient = new()
                    {
                        ID = clientID,
                        Name = CheckClientName(clientName, clientID, false),
                        Address = new IPAddress(clientAddress),
                        TcpPort = clientTcpPort,
                        UdpPort = clientUdpPort,
                        Self = false,
                        Host = false,
                    };
                    _clients.Add(clientID, incommingClient);
                    OnClientConnected?.Invoke(incommingClient);

                    syncClients = _clients.Values.ToArray();

                    //diffuse new client (exept host AND incomming)
                    foreach (var client in _clients.Values)
                    {
                        if (client.ID == incommingClient.ID) continue;
                        if (client.Host) continue;

                        Send(ADD_CLIENT_CODE, client.TcpIP, incommingClient);
                    }
                }
                else info = JoinRequestAnswerInfo.Denied_MaxClientCount;
            }
            else info = JoinRequestAnswerInfo.Denied;


            var incommingIp = new IPEndPoint(new IPAddress(clientAddress), clientTcpPort);

            //answer join request
            Send(JOIN_ANSWER_CODE, incommingIp, new object[]
            {
                (byte)info, clientID, maxClient, syncClients
            });
        }
        void OnJoinAnswer(Packet packet)
        {
            //info, clientID, clientName, maxClient, syncClients
            Serializer serializer = new(packet.Bytes);
            var info = (JoinRequestAnswerInfo)serializer.GetNextItem<byte>();
            var clientID = serializer.GetNextItem<int>();
            var maxClient = serializer.GetNextItem<int>();
            var syncClients = serializer.GetNextItem<Client[]>();

            if (info == JoinRequestAnswerInfo.Accepted)
            {
                _connectedToServer = true;
                _clients.Clear();
                _maxClient = maxClient;

                foreach (var client in syncClients)
                {
                    client.Self = client.ID == clientID;

                    if (client.Self)
                    {
                        _selfClientID = client.ID;
                    }

                    _clients.Add(client.ID, client);
                }
            }

            OnJoinRequestAnswer?.Invoke(info);
        }
        void OnAddClient(Packet packet)
        {
            var client = Serializer.DeserializeItem<Client>(packet.Bytes);
            _clients.Add(client.ID, client);
            OnClientConnected?.Invoke(client);
        }
        void OnRemoveClient(Packet packet)
        {
            var clientID = Serializer.DeserializeItem<int>(packet.Bytes);
            var info = (DisconnectInfo)Serializer.DeserializeItem<byte>(packet.Bytes);
            var client = _clients[clientID];
            _clients.Remove(clientID);
            OnClientDisconnected?.Invoke(client, info);
        }
    }
}
