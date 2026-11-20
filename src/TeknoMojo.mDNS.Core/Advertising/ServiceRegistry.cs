namespace TeknoMojo.mDNS.Core.Advertising;

/// <summary>
/// Thread-safe registry of services being advertised
/// </summary>
public class ServiceRegistry
{
    private readonly ConcurrentDictionary<string, ServiceInfo> _services = new();

    /// <summary>
    /// Register a service
    /// </summary>
    public void Register(ServiceInfo serviceInfo)
    {
        if (serviceInfo == null)
            throw new ArgumentNullException(nameof(serviceInfo));

        var key = serviceInfo.FullName.ToLowerInvariant();
        _services[key] = serviceInfo;
    }

    /// <summary>
    /// Unregister a service
    /// </summary>
    public bool Unregister(string fullName)
    {
        var key = fullName.ToLowerInvariant();
        return _services.TryRemove(key, out _);
    }

    /// <summary>
    /// Get a service by full name
    /// </summary>
    public ServiceInfo? Get(string fullName)
    {
        var key = fullName.ToLowerInvariant();
        _services.TryGetValue(key, out var service);
        return service;
    }

    /// <summary>
    /// Get all services of a specific type
    /// </summary>
    public IEnumerable<ServiceInfo> GetByServiceType(string serviceType)
    {
        var lowerType = serviceType.ToLowerInvariant();
        return _services.Values
            .Where(s => s.ServiceType.Equals(lowerType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all registered services
    /// </summary>
    public IEnumerable<ServiceInfo> GetAll()
    {
        return _services.Values;
    }

    /// <summary>
    /// Clear all registered services
    /// </summary>
    public void Clear()
    {
        _services.Clear();
    }

    /// <summary>
    /// Get the number of registered services
    /// </summary>
    public int Count => _services.Count;
}
