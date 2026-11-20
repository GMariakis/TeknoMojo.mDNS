namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Helper class providing common service discovery operations
/// </summary>
public class ServiceDiscoveryHelper
{
    private readonly BackgroundDiscoveryService _discovery;
    private readonly ILogger<ServiceDiscoveryHelper> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceDiscoveryHelper"/> class.
    /// </summary>
    /// <param name="discovery">The background discovery service instance</param>
    /// <param name="logger">The logger instance for this helper</param>
    /// <exception cref="ArgumentNullException">Thrown when discovery or logger is null</exception>
    public ServiceDiscoveryHelper(BackgroundDiscoveryService discovery, ILogger<ServiceDiscoveryHelper> logger)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Browse for multiple service types concurrently and return deduplicated results
    /// </summary>
    /// <param name="serviceTypes">Array of service types to query</param>
    /// <param name="timeout">Timeout for each service type query</param>
    /// <param name="includeCurrentServices">Whether to include currently cached services</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deduplicated collection of discovered services</returns>
    public async Task<IReadOnlyCollection<ServiceInstance>> BrowseMultipleAsync(
        string[] serviceTypes,
        TimeSpan timeout,
        bool includeCurrentServices = true,
        CancellationToken cancellationToken = default)
    {
        var allServices = new List<ServiceInstance>();

        // Query each service type
        var tasks = serviceTypes.Select(async serviceType =>
        {
            try
            {
                var services = await _discovery.BrowseAsync(serviceType, timeout, cancellationToken).ConfigureAwait(false);
                return services;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to browse for service type: {ServiceType}", serviceType);
                return Enumerable.Empty<ServiceInstance>();
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results)
        {
            allServices.AddRange(result);
        }

        // Include currently cached services if requested
        if (includeCurrentServices)
        {
            allServices.AddRange(_discovery.CurrentServices);
        }

        // Deduplicate by FullName
        return allServices
            .DistinctBy(s => s.FullName)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Browse for all common service types on the network
    /// </summary>
    /// <param name="useQuickScan">If true, uses MostCommon service types; otherwise uses AllCommon</param>
    /// <param name="timeout">Timeout for each service type query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deduplicated collection of discovered services</returns>
    public async Task<IReadOnlyCollection<ServiceInstance>> BrowseCommonServicesAsync(
        bool useQuickScan = true,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var serviceTypes = useQuickScan ? CommonServiceTypes.MostCommon : CommonServiceTypes.AllCommon;
        var queryTimeout = timeout ?? TimeSpan.FromSeconds(3);

        return await BrowseMultipleAsync(serviceTypes, queryTimeout, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Groups services by their service type
    /// </summary>
    /// <param name="services">Collection of services to group</param>
    /// <returns>Dictionary with service type as key and list of services as value</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<ServiceInstance>> GroupByServiceType(
        IEnumerable<ServiceInstance> services)
    {
        return services
            .GroupBy(s => s.ServiceType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ServiceInstance>)g.OrderBy(s => s.InstanceName).ToList().AsReadOnly(),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets statistics about discovered services
    /// </summary>
    /// <param name="services">Collection of services to analyze</param>
    /// <returns>Service statistics</returns>
    public ServiceStatistics GetStatistics(IEnumerable<ServiceInstance> services)
    {
        var serviceList = services.ToList();
        var groupedByType = GroupByServiceType(serviceList);

        return new ServiceStatistics
        {
            TotalServices = serviceList.Count,
            TotalServiceTypes = groupedByType.Count,
            ResolvedServices = serviceList.Count(s => s.IsResolved),
            UnresolvedServices = serviceList.Count(s => !s.IsResolved),
            ServicesByType = groupedByType.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Count,
                StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    /// Filters services by protocol type (TCP or UDP)
    /// </summary>
    /// <param name="services">Collection of services to filter</param>
    /// <param name="protocol">Protocol to filter by ("tcp" or "udp")</param>
    /// <returns>Filtered collection of services</returns>
    public IReadOnlyCollection<ServiceInstance> FilterByProtocol(
        IEnumerable<ServiceInstance> services,
        string protocol)
    {
        var protocolFilter = $"._{protocol.ToLowerInvariant()}.";
        return services
            .Where(s => s.ServiceType.Contains(protocolFilter, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets only fully resolved services
    /// </summary>
    /// <param name="services">Collection of services to filter</param>
    /// <returns>Collection of resolved services</returns>
    public IReadOnlyCollection<ServiceInstance> GetResolvedServices(IEnumerable<ServiceInstance> services)
    {
        return services
            .Where(s => s.IsResolved)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Searches for services by name pattern
    /// </summary>
    /// <param name="services">Collection of services to search</param>
    /// <param name="pattern">Search pattern (case-insensitive)</param>
    /// <returns>Collection of matching services</returns>
    public IReadOnlyCollection<ServiceInstance> SearchByName(
        IEnumerable<ServiceInstance> services,
        string pattern)
    {
        return services
            .Where(s => s.InstanceName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets all services running on a specific hostname
    /// </summary>
    /// <param name="hostname">The hostname to filter by (case-insensitive)</param>
    /// <param name="includeUnresolved">Whether to include services that don't have hostname resolved yet</param>
    /// <returns>Collection of services on the specified hostname, sorted by service type and instance name</returns>
    public IReadOnlyCollection<ServiceInstance> GetServicesByHostname(
        string hostname,
        bool includeUnresolved = false)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("Hostname cannot be null or whitespace", nameof(hostname));

        return _discovery.CurrentServices
            .Where(s => !string.IsNullOrEmpty(s.HostName) &&
                        s.HostName.Equals(hostname, StringComparison.OrdinalIgnoreCase) &&
                        (includeUnresolved || s.IsResolved))
            .OrderBy(s => s.ServiceType)
            .ThenBy(s => s.InstanceName)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets all unique hostnames from discovered services
    /// </summary>
    /// <param name="resolvedOnly">If true, only includes hostnames from resolved services</param>
    /// <returns>Collection of unique hostnames, sorted alphabetically</returns>
    public IReadOnlyCollection<string> GetDiscoveredHostnames(bool resolvedOnly = true)
    {
        return _discovery.CurrentServices
            .Where(s => !string.IsNullOrEmpty(s.HostName) &&
                        (!resolvedOnly || s.IsResolved))
            .Select(s => s.HostName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(h => h)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Groups all discovered services by their hostname
    /// </summary>
    /// <param name="resolvedOnly">If true, only includes resolved services</param>
    /// <returns>Dictionary with hostname as key and list of services as value</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<ServiceInstance>> GroupByHostname(bool resolvedOnly = true)
    {
        return _discovery.CurrentServices
            .Where(s => !string.IsNullOrEmpty(s.HostName) &&
                        (!resolvedOnly || s.IsResolved))
            .GroupBy(s => s.HostName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ServiceInstance>)g
                    .OrderBy(s => s.ServiceType)
                    .ThenBy(s => s.InstanceName)
                    .ToList()
                    .AsReadOnly(),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all services of a specific service type from the cache
    /// </summary>
    /// <param name="serviceType">The service type to filter by (e.g., _smb._tcp.local, case-insensitive)</param>
    /// <param name="includeUnresolved">Whether to include services that aren't fully resolved yet</param>
    /// <returns>Collection of services of the specified type, sorted by instance name</returns>
    public IReadOnlyCollection<ServiceInstance> GetServicesByType(
        string serviceType,
        bool includeUnresolved = false)
    {
        if (string.IsNullOrWhiteSpace(serviceType))
            throw new ArgumentException("Service type cannot be null or whitespace", nameof(serviceType));

        return _discovery.CurrentServices
            .Where(s => !string.IsNullOrEmpty(s.ServiceType) &&
                        s.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase) &&
                        (includeUnresolved || s.IsResolved))
            .OrderBy(s => s.InstanceName)
            .ThenBy(s => s.HostName)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets all unique service types from cached discovered services
    /// </summary>
    /// <param name="resolvedOnly">If true, only includes service types from resolved services</param>
    /// <returns>Collection of unique service types, sorted alphabetically</returns>
    public IReadOnlyCollection<string> GetDiscoveredServiceTypes(bool resolvedOnly = true)
    {
        return _discovery.CurrentServices
            .Where(s => !string.IsNullOrEmpty(s.ServiceType) &&
                        (!resolvedOnly || s.IsResolved))
            .Select(s => s.ServiceType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets all SignalR services from the cache
    /// </summary>
    /// <param name="resolvedOnly">If true, only includes fully resolved services</param>
    /// <returns>Collection of SignalR services, sorted by instance name</returns>
    public IReadOnlyCollection<ServiceInstance> GetSignalRServices(bool resolvedOnly = true)
    {
        return _discovery.CurrentServices
            .Where(s => s.ServiceType.Contains("_signalr._tcp", StringComparison.OrdinalIgnoreCase) &&
                        (!resolvedOnly || s.IsResolved))
            .OrderBy(s => s.InstanceName)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Browse for SignalR services on the network
    /// </summary>
    /// <param name="timeout">Timeout for the query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of discovered SignalR services</returns>
    public async Task<IReadOnlyCollection<ServiceInstance>> BrowseSignalRServicesAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var queryTimeout = timeout ?? SignalRServiceHelper.Constants.DefaultBrowseTimeout;

        try
        {
            var services = await _discovery.BrowseAsync(
                SignalRServiceHelper.Constants.ServiceType,
                queryTimeout,
                cancellationToken).ConfigureAwait(false);
            return services.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to browse for SignalR services");
            return Array.Empty<ServiceInstance>();
        }
    }

    /// <summary>
    /// Gets SignalR connection information from a service instance
    /// </summary>
    /// <param name="service">The SignalR service instance</param>
    /// <param name="useSecure">If true, uses HTTPS/WSS instead of HTTP/WS</param>
    /// <returns>SignalR connection info, or null if service is not valid</returns>
    public SignalRConnectionInfo? GetSignalRConnectionInfo(ServiceInstance service, bool useSecure = false)
    {
        if (!SignalRServiceHelper.IsValidSignalRService(service))
            return null;

        var address = service.Addresses!.First();

        return new SignalRConnectionInfo
        {
            ServiceName = service.InstanceName,
            Host = address.ToString(),
            Port = service.Port,
            HubPath = SignalRServiceHelper.GetHubPath(service),
            Url = SignalRServiceHelper.BuildHttpUrl(service, useSecure)!,
            WebSocketUrl = SignalRServiceHelper.BuildWebSocketUrl(service, useSecure)!,
            HealthCheckUrl = SignalRServiceHelper.BuildHealthCheckUrl(service, useSecure)!,
            InfoUrl = SignalRServiceHelper.BuildInfoUrl(service, useSecure)!,
            Features = SignalRServiceHelper.GetFeatures(service),
            Version = SignalRServiceHelper.GetVersion(service)
        };
    }
}

/// <summary>
/// Statistics about discovered services
/// </summary>
public class ServiceStatistics
{
    /// <summary>
    /// Total number of services discovered
    /// </summary>
    public int TotalServices { get; init; }

    /// <summary>
    /// Total number of unique service types
    /// </summary>
    public int TotalServiceTypes { get; init; }

    /// <summary>
    /// Number of fully resolved services
    /// </summary>
    public int ResolvedServices { get; init; }

    /// <summary>
    /// Number of unresolved services
    /// </summary>
    public int UnresolvedServices { get; init; }

    /// <summary>
    /// Count of services grouped by type
    /// </summary>
    public IReadOnlyDictionary<string, int> ServicesByType { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Percentage of services that are fully resolved
    /// </summary>
    public double ResolvedPercentage =>
        TotalServices > 0 ? (ResolvedServices * 100.0 / TotalServices) : 0;
}

/// <summary>
/// Connection information for a SignalR service
/// </summary>
public class SignalRConnectionInfo
{
    /// <summary>
    /// Name of the SignalR service
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Host IP address or hostname
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Port number
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Hub path (e.g., "/notification")
    /// </summary>
    public required string HubPath { get; init; }

    /// <summary>
    /// Full HTTP connection URL (for SignalR client connection)
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// WebSocket URL for the SignalR hub
    /// </summary>
    public required string WebSocketUrl { get; init; }

    /// <summary>
    /// Health check endpoint URL
    /// </summary>
    public required string HealthCheckUrl { get; init; }

    /// <summary>
    /// Info endpoint URL
    /// </summary>
    public required string InfoUrl { get; init; }

    /// <summary>
    /// Available features (from TXT records)
    /// </summary>
    public string[] Features { get; init; } = [];

    /// <summary>
    /// Service version
    /// </summary>
    public string Version { get; init; } = "1.0.0";
}
