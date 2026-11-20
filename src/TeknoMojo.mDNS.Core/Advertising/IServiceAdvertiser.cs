namespace TeknoMojo.mDNS.Core.Advertising;

/// <summary>
/// Interface for advertising services via mDNS
/// </summary>
public interface IServiceAdvertiser : IDisposable
{
    /// <summary>
    /// Start the service advertiser (join multicast group, start listening)
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the service advertiser
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Register a service for advertisement
    /// </summary>
    Task RegisterServiceAsync(ServiceInfo serviceInfo);

    /// <summary>
    /// Unregister a service (sends goodbye packet)
    /// </summary>
    Task UnregisterServiceAsync(string instanceName);

    /// <summary>
    /// Update a service's information
    /// </summary>
    Task UpdateServiceAsync(ServiceInfo serviceInfo);
}
