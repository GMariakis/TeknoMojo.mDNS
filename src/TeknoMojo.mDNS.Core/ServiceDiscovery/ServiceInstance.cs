namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Represents a discovered service instance on the network
/// </summary>
public class ServiceInstance
{
    /// <summary>
    /// Full service instance name (e.g., "My Web Server._http._tcp.local")
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly instance name (e.g., "My Web Server")
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// Service type (e.g., "_http._tcp.local")
    /// </summary>
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>
    /// Hostname where the service is located
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Port number where the service is available
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// IP addresses where the service can be reached
    /// </summary>
    public IPAddress[] Addresses { get; set; } = Array.Empty<IPAddress>();

    /// <summary>
    /// Additional properties/metadata from TXT records
    /// </summary>
    public Dictionary<string, string> TxtRecords { get; set; } = new();

    /// <summary>
    /// Timestamp when this service was last seen
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time-to-live in seconds from DNS records (default 120 seconds)
    /// </summary>
    public uint Ttl { get; set; } = 120;

    /// <summary>
    /// Calculated expiration time based on LastSeen + TTL
    /// </summary>
    public DateTime ExpiresAt => LastSeen.AddSeconds(Ttl);

    /// <summary>
    /// Indicates if this service has expired based on its TTL
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Indicates if this service has been fully resolved (has hostname, port, and addresses)
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// Priority from SRV record
    /// </summary>
    public ushort Priority { get; set; }

    /// <summary>
    /// Weight from SRV record
    /// </summary>
    public ushort Weight { get; set; }

    public ServiceInstance()
    {
    }

    public ServiceInstance(string fullName, string instanceName, string serviceType)
    {
        FullName = fullName;
        InstanceName = instanceName;
        ServiceType = serviceType;
    }

    public override string ToString()
    {
        if (IsResolved)
        {
            var addresses = Addresses.Length > 0 ? string.Join(", ", Addresses.Select(a => a.ToString())) : "none";
            return $"{InstanceName} at {HostName}:{Port} ({addresses})";
        }
        else
        {
            return $"{InstanceName} (not resolved)";
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is ServiceInstance other && FullName == other.FullName;
    }

    public override int GetHashCode()
    {
        return FullName.GetHashCode();
    }
}
