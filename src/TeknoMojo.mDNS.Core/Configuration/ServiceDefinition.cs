namespace TeknoMojo.mDNS.Core.Configuration;

/// <summary>
/// Defines a service to be advertised
/// </summary>
public class ServiceDefinition
{
    /// <summary>
    /// User-friendly service name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Service type (e.g., "_http._tcp")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Port number
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol (http, tcp, udp)
    /// </summary>
    public string Protocol { get; set; } = "tcp";

    /// <summary>
    /// Whether this service is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Additional properties (TXT records)
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();
}
