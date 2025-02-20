# P2PNet

## Overview
P2PNet is a peer-to-peer networking library for establishing direct communication between multiple clients without the need for a central server. It provides TCP and UDP-based messaging, client management, and secure invite-based connections.

## Features
- Peer-to-peer communication over TCP and UDP
- Event-driven architecture for packet reception and client connections
- Secure server invite codes with encryption
- Custom serialization for efficient data transmission
- Threaded network handling for asynchronous processing

## Getting Started

### Prerequisites
- .NET Framework or .NET Core
- Unity (Optional, if used in a game project)

### Installation
Clone the repository:
```sh
git clone https://github.com/abde-bout/P2PNet.git
```

Include the `P2PNet` namespace in your project:
```csharp
using P2PNet;
```

### Usage

#### Hosting a Server
```csharp
P2PService p2pService = new P2PService();
p2pService.HostServer("MyServer", 10);
string inviteCode = p2pService.GetServerCode();
```

#### Connecting to a Server
```csharp
p2pService.ConnectToServer("ClientName", "InviteCode");
```

#### Sending Data
```csharp
p2pService.Send(P2PService.DATA_CODE, Filter.All, "Hello, Peers!");
```

#### Handling Events
```csharp
p2pService.OnPacketReception += packet => Console.WriteLine("Received: " + packet.Data);
p2pService.OnClientConnected += client => Console.WriteLine(client.Name + " connected");
```

## API Reference

### Public Properties
- `Status` - Current service status
- `ConnectedToServer` - Whether connected to a host
- `ServerIsActive` - Whether the server is active
- `MaxClient` - Maximum allowed clients
- `Clients` - List of connected clients

### Public Methods
- `HostServer(string name, int maxClient)` - Hosts a server
- `ConnectToServer(string name, string code)` - Connects to a server
- `Send(Code code, Filter filter, params object[] args)` - Sends data to clients
- `GetServerCode()` - Generates a server invite code

## License
This project is licensed under the MIT License.

## Contact
For questions or issues, open an issue on GitHub.

