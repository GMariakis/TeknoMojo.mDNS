using System.Net.NetworkInformation;

namespace TeknoMojo.mDNS.Core.Configuration;

/// <summary>
/// Configuration options for mDNS service discovery
/// </summary>
public class DiscoveryOptions
{
    /// <summary>
    /// Restricts multicast send/receive to the network interfaces with these IPv4 interface
    /// indices. When <c>null</c> or empty (the default), every eligible interface is used
    /// (operationally up, multicast-capable, non-loopback, with an IPv4 address) — the same
    /// "query every link" behavior as Bonjour/Avahi, which is what works on multi-homed hosts
    /// (VPNs, Tailscale, VMs). Provide indices only to deliberately narrow discovery to specific
    /// NICs. See <see cref="NetworkInterfaceFilter"/> for predicate-based selection.
    /// </summary>
    public IReadOnlyList<int>? NetworkInterfaceIndices { get; set; }

    /// <summary>
    /// Optional predicate to further narrow which interfaces are used, applied after the built-in
    /// eligibility check and any <see cref="NetworkInterfaceIndices"/>. When <c>null</c> (the
    /// default) no extra filtering is applied. This is a general mechanism (e.g. "Wi-Fi only",
    /// "a specific subnet") — the library never hard-codes vendor or adapter names.
    /// </summary>
    public Func<NetworkInterface, bool>? NetworkInterfaceFilter { get; set; }

    /// <summary>
    /// Duration to actively listen for mDNS traffic before entering sleep mode.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan ListenDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Duration to sleep between active listening periods.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan SleepDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether to use duty cycling (listen/sleep pattern) to reduce CPU usage.
    /// If false, continuously listens for traffic.
    /// Default is false.
    /// </summary>
    public bool UseDutyCycling { get; set; } = false;

    /// <summary>
    /// Interval between sending query probes when browsing for services.
    /// Default is 2 seconds.
    /// </summary>
    public TimeSpan QueryInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Number of times to repeat a query to ensure discovery.
    /// Default is 3.
    /// </summary>
    public int QueryRetryCount { get; set; } = 3;

    /// <summary>
    /// Service types to automatically browse for on startup.
    /// If empty, no automatic browsing is performed.
    /// </summary>
    public List<string> AutoBrowseServiceTypes { get; set; } = new();

    /// <summary>
    /// Whether to automatically resolve services when discovered.
    /// If false, only PTR records are cached until explicit resolution.
    /// Default is true.
    /// </summary>
    public bool AutoResolve { get; set; } = true;

    /// <summary>
    /// Time after which a service is considered lost if not seen.
    /// Default is 75% of the TTL or 2 minutes, whichever is less.
    /// </summary>
    public TimeSpan ServiceTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Enable background discovery that continuously queries for services.
    /// Default is false.
    /// </summary>
    public bool EnableBackgroundDiscovery { get; set; } = false;

    /// <summary>
    /// Interval between background discovery scans.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan BackgroundScanInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Duration of each background discovery scan (how long to wait for responses).
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan BackgroundScanDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Common service types to query during background discovery.
    /// These are well-known mDNS/Bonjour service types.
    /// </summary>
    public List<string> BackgroundServiceTypes { get; set; } = new()
    {
        "_services._dns-sd._udp.local",  // DNS-SD meta-query for all service types
        "_http._tcp.local",
        "_https._tcp.local",
        "_printer._tcp.local",
        "_ipp._tcp.local",
        "_airplay._tcp.local",
        "_raop._tcp.local",
        "_googlecast._tcp.local",
        "_spotify-connect._tcp.local",
        "_smb._tcp.local",
        "_afpovertcp._tcp.local",
        "_ssh._tcp.local",
        "_sftp-ssh._tcp.local",
        "_ftp._tcp.local"
    };
}
