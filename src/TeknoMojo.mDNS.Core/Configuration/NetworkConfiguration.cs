namespace TeknoMojo.mDNS.Core.Configuration;

/// <summary>
/// Network-related configuration settings
/// </summary>
public class NetworkConfiguration
{
    /// <summary>
    /// Hostname for this machine (e.g., "myserver.local")
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Network interfaces to use (* for all)
    /// </summary>
    public List<string> NetworkInterfaces { get; set; } = new() { "*" };

    /// <summary>
    /// Whether to use IPv6
    /// </summary>
    public bool UseIPv6 { get; set; } = false;

    /// <summary>
    /// IP time to live for multicast packets
    /// </summary>
    public int TimeToLive { get; set; } = 255;
}
