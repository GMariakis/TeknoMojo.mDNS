namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Configuration model for defining a service to advertise
/// </summary>
public class ServiceInfo
{
    /// <summary>
    /// User-friendly instance name (e.g., "My Web Server")
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// Service type (e.g., "_http._tcp.local")
    /// </summary>
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>
    /// Hostname (e.g., "myserver.local")
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Port number where the service is available
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Additional properties/metadata for the service (TXT records)
    /// </summary>
    public Dictionary<string, string> TxtRecords { get; set; } = new();

    /// <summary>
    /// IP addresses where the service can be reached
    /// </summary>
    public IPAddress[] Addresses { get; set; } = Array.Empty<IPAddress>();

    /// <summary>
    /// Gets the full service instance name
    /// </summary>
    public string FullName => $"{InstanceName}.{ServiceType}";

    public ServiceInfo()
    {
    }

    public ServiceInfo(string instanceName, string serviceType, string hostName, int port)
    {
        InstanceName = instanceName;
        ServiceType = serviceType;
        HostName = hostName;
        Port = port;
    }

    public override string ToString()
    {
        return $"{InstanceName}.{ServiceType} at {HostName}:{Port}";
    }
}
