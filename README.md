# mDNS Service Discovery

A complete mDNS (Multicast DNS) service discovery implementation for .NET 11, similar to Apple's Bonjour. This library allows services to advertise themselves on the local network and enables clients to discover and connect to those services automatically.

## Features

- 🔍 **Service Discovery**: Browse and discover services on the local network
- 📢 **Service Advertisement**: Advertise services with custom properties
- 🔌 **Protocol Support**: HTTP, HTTPS, TCP, UDP, and custom protocols
- 🔗 **Automatic Connection**: Discover and connect to services seamlessly
- ⚙️ **Configurable**: JSON-based configuration for easy service setup
- 🌐 **Standards Compliant**: Implements RFC 6762 (mDNS) and RFC 6763 (DNS-SD)
- 🚀 **Async/Await**: Modern async patterns throughout
- 📝 **Strongly Typed**: Full C# 14 type safety
- 💼 **Working Examples**: Fully functional HTTP, TCP, and UDP service implementations

## Technology Stack

- **.NET**: 11
- **C#**: 14
- **Protocols**: mDNS (RFC 6762), DNS-SD (RFC 6763)

## Project Structure

```
mDNS/
├── src/
│   ├── mDNS.Core/              # Core library
│   │   ├── Protocol/           # DNS packet handling
│   │   ├── Transport/          # Network communication
│   │   ├── ServiceDiscovery/   # Discovery API
│   │   ├── Advertising/        # Advertisement API
│   │   ├── Cache/             # DNS record caching
│   │   └── Configuration/      # Configuration models
│   ├── mDNS.ServiceHost/       # Service advertiser demo
│   └── mDNS.Client/            # Service discovery demo
└── tests/
    └── mDNS.Core.Tests/        # Unit tests (to be added)
```

## Quick Start

### 1. Build the Solution

```bash
dotnet build
```

### 2. Run the Service Host

In one terminal:

```bash
cd src/mDNS.ServiceHost
dotnet run
```

This will advertise and run the services defined in `appsettings.json`:
- **HTTP service** on port 8080 - Web server that displays service information
- **TCP service** on port 2121 - Echo server for interactive messaging
- **UDP service** on port 5000 - UDP echo server

These are fully functional services that clients can connect to and interact with.

### 3. Run the Client

In another terminal:

```bash
cd src/mDNS.Client
dotnet run
```

Use the interactive menu to browse for services:
1. Browse for HTTP services
2. Browse for FTP services
3. Browse for custom service types
4. Exit

After browsing, you can connect to any discovered service by selecting its number. The client will automatically establish the appropriate connection type (HTTP, TCP, or UDP) based on the service type.

## Using the Library

### Service Advertisement

```csharp
using mDNS.Core.Advertising;
using mDNS.Core.ServiceDiscovery;

// Create advertiser
using var advertiser = new ServiceAdvertiser();
await advertiser.StartAsync();

// Define a service
var serviceInfo = new ServiceInfo
{
    InstanceName = "My Web Server",
    ServiceType = "_http._tcp.local",
    HostName = "myserver.local",
    Port = 8080,
    TxtRecords = new Dictionary<string, string>
    {
        { "version", "1.0" },
        { "path", "/api" }
    },
    Addresses = new[] { IPAddress.Parse("192.0.2.100") }
};

// Register the service
await advertiser.RegisterServiceAsync(serviceInfo);

// Keep running...
Console.ReadLine();

// Stop (sends goodbye packets)
await advertiser.StopAsync();
```

### Service Discovery

```csharp
using mDNS.Core.ServiceDiscovery;

// Create browser
using var browser = new ServiceBrowser();
await browser.StartAsync();

// Browse for HTTP services
var services = await browser.BrowseAsync(
    "_http._tcp.local",
    TimeSpan.FromSeconds(5));

foreach (var service in services)
{
    Console.WriteLine($"Found: {service.InstanceName}");
    Console.WriteLine($"  at {service.HostName}:{service.Port}");

    if (service.Addresses?.Length > 0)
    {
        Console.WriteLine($"  IP: {service.Addresses[0]}");
    }

    foreach (var prop in service.TxtRecords)
    {
        Console.WriteLine($"  {prop.Key}: {prop.Value}");
    }
}

await browser.StopAsync();
```

### Continuous Monitoring

```csharp
using var browser = new ServiceBrowser();

// Subscribe to events
browser.ServiceFound += (sender, service) =>
{
    Console.WriteLine($"Service appeared: {service.InstanceName}");
};

browser.ServiceLost += (sender, service) =>
{
    Console.WriteLine($"Service disappeared: {service.InstanceName}");
};

await browser.StartAsync();

// Monitor asynchronously
await foreach (var service in browser.BrowseAsyncStream("_http._tcp.local"))
{
    Console.WriteLine($"Discovered: {service.InstanceName}");
}
```

## Configuration

### Service Host Configuration (appsettings.json)

```json
{
  "mDNS": {
    "network": {
      "hostName": "myserver.local",
      "networkInterfaces": ["*"],
      "useIPv6": false
    },
    "services": [
      {
        "name": "My Web Server",
        "type": "_http._tcp",
        "port": 8080,
        "enabled": true,
        "properties": {
          "version": "1.0",
          "path": "/api"
        }
      }
    ]
  }
}
```

## Service Types

Common service types (following DNS-SD conventions):

- `_http._tcp.local` - HTTP services
- `_https._tcp.local` - HTTPS services
- `_ftp._tcp.local` - FTP services
- `_ssh._tcp.local` - SSH services
- `_printer._tcp.local` - Printers
- `_airplay._tcp.local` - AirPlay
- `_myapp._tcp.local` - Custom TCP application
- `_myapp._udp.local` - Custom UDP application

## How It Works

### mDNS Protocol

mDNS uses IP multicast to send DNS queries and responses:
- **Multicast Address**: 224.0.0.251 (IPv4) / ff02::fb (IPv6)
- **Port**: 5353
- **Transaction ID**: 0 for mDNS

### Service Advertisement Flow

1. Service registers with `ServiceAdvertiser`
2. Initial announcements sent at t=0s, t=1s, t=2s
3. Listens for queries from clients
4. Responds with PTR, SRV, TXT, and A/AAAA records
5. Sends goodbye (TTL=0) on shutdown

### Service Discovery Flow

1. Client sends PTR query for service type (e.g., "_http._tcp.local")
2. Services respond with their instance names
3. Client resolves each instance (SRV for port, TXT for properties, A/AAAA for addresses)
4. Client connects to discovered service

### DNS Records Used

- **PTR**: Points from service type to instance names
- **SRV**: Contains hostname and port
- **TXT**: Contains key-value properties
- **A/AAAA**: Contains IPv4/IPv6 addresses

## Architecture

### Key Components

- **DnsPacketBuilder**: Serializes DNS messages to bytes with name compression
- **DnsPacketParser**: Deserializes DNS packets from bytes
- **MulticastSocket**: Handles UDP multicast communication
- **ServiceBrowser**: Discovers services on the network
- **ServiceAdvertiser**: Advertises services on the network
- **RecordCache**: TTL-based caching of DNS records

### Design Patterns

- **Builder Pattern**: DNS packet construction
- **Observer Pattern**: Service found/lost events
- **Repository Pattern**: Service registry
- **Async Streams**: Continuous service monitoring

## Requirements

- .NET 11 SDK
- Network with multicast support
- Firewall configured to allow UDP port 5353

## Troubleshooting

### Services Not Discovered

1. Check firewall settings (allow UDP 5353)
2. Ensure multicast is enabled on your network
3. Verify both applications are on the same network
4. Check antivirus/security software

### Build Issues

If you encounter .NET version issues:

```bash
dotnet --version  # Should show 11.x.x
```

Install .NET 11 SDK from: https://dotnet.microsoft.com/download/dotnet/11.0

## License

This project is for educational and demonstration purposes.

## References

- [RFC 6762 - Multicast DNS](https://datatracker.ietf.org/doc/html/rfc6762)
- [RFC 6763 - DNS-Based Service Discovery](https://datatracker.ietf.org/doc/html/rfc6763)
- [DNS-SD](http://www.dns-sd.org/)

## Author

Created with Claude Code - Demonstrating mDNS/Bonjour-like service discovery in .NET 11.
