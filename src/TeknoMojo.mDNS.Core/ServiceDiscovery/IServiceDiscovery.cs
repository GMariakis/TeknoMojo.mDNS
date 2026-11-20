namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Main interface for mDNS service discovery operations
/// </summary>
public interface IServiceDiscovery : IDisposable
{
    /// <summary>
    /// Start the service discovery (join multicast group, start listening)
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the service discovery
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Browse for services of a specific type with a timeout
    /// </summary>
    /// <param name="serviceType">Service type to browse for (e.g., "_http._tcp.local")</param>
    /// <param name="timeout">How long to wait for responses</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of discovered services</returns>
    Task<IEnumerable<ServiceInstance>> BrowseAsync(string serviceType, TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Browse for services continuously as an async stream
    /// </summary>
    IAsyncEnumerable<ServiceInstance> BrowseAsyncStream(string serviceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve full details of a specific service instance
    /// </summary>
    Task<ServiceInstance?> ResolveAsync(string instanceName, string serviceType, TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Observable stream of all service discovery events.
    /// Subscribe to receive Found, Updated, Lost, and TimedOut events.
    /// </summary>
    IObservable<ServiceDiscoveryEvent> ServiceEvents { get; }

    /// <summary>
    /// Observable stream of currently discovered services.
    /// Emits the current set of services whenever a change occurs.
    /// </summary>
    IObservable<IReadOnlyCollection<ServiceInstance>> Services { get; }

    /// <summary>
    /// Gets the current snapshot of all discovered services
    /// </summary>
    IReadOnlyCollection<ServiceInstance> CurrentServices { get; }

    /// <summary>
    /// Raised when a new service is found
    /// </summary>
    event EventHandler<ServiceInstance>? ServiceFound;

    /// <summary>
    /// Raised when a service is lost (goodbye or timeout)
    /// </summary>
    event EventHandler<ServiceInstance>? ServiceLost;
}
