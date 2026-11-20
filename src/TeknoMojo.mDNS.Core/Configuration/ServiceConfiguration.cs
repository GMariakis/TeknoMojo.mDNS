namespace TeknoMojo.mDNS.Core.Configuration;

/// <summary>
/// Root configuration for mDNS service host
/// </summary>
public class ServiceConfiguration
{
    /// <summary>
    /// Network configuration
    /// </summary>
    public NetworkConfiguration Network { get; set; } = new();

    /// <summary>
    /// Services to advertise
    /// </summary>
    public List<ServiceDefinition> Services { get; set; } = new();
}
