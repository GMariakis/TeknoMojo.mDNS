// Global using directives for TeknoMojo.mDNS.Core

// Microsoft Extensions
global using Microsoft.Extensions.Hosting;

// System namespaces
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using System.Collections.Concurrent;
global using System.Net;
global using System.Net.NetworkInformation;
global using System.Net.Sockets;
global using System.Reactive.Linq;
global using System.Reactive.Subjects;
global using System.Runtime.CompilerServices;
global using System.Text;

// TeknoMojo.mDNS.Core
global using TeknoMojo.mDNS.Core.Advertising;
global using TeknoMojo.mDNS.Core.Cache;
global using TeknoMojo.mDNS.Core.Configuration;
global using TeknoMojo.mDNS.Core.Protocol;
global using TeknoMojo.mDNS.Core.Protocol.Constants;
global using TeknoMojo.mDNS.Core.Protocol.RecordTypes;

// Keep the mDNS wire record types distinct from the DNS types in System.Net.
global using PtrRecord = TeknoMojo.mDNS.Core.Protocol.RecordTypes.PtrRecord;
global using SrvRecord = TeknoMojo.mDNS.Core.Protocol.RecordTypes.SrvRecord;
global using TxtRecord = TeknoMojo.mDNS.Core.Protocol.RecordTypes.TxtRecord;
global using TeknoMojo.mDNS.Core.ServiceDiscovery;
global using TeknoMojo.mDNS.Core.Transport;
global using TeknoMojo.mDNS.Core.Utilities;
