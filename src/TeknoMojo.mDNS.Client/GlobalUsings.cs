// Global using directives for TeknoMojo.mDNS.Client

// Microsoft Extensions
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

//Serilog
global using Serilog;

// System namespaces
global using System.Net.Sockets;
global using System.Reactive.Linq;
global using System.Text;

// MessagePack
global using MessagePack;

// TeknoMojo.mDNS.Core
global using TeknoMojo.mDNS.Core.Configuration;
global using TeknoMojo.mDNS.Core.Connectivity;
global using TeknoMojo.mDNS.Core.ServiceDiscovery;
global using TeknoMojo.mDNS.Core.Utilities;

// TeknoMojo.mDNS.Messages
global using TeknoMojo.mDNS.Messages;

// TeknoMojo.mDNS.Client (for SignalRConnector and other client classes)
global using TeknoMojo.mDNS.Client;