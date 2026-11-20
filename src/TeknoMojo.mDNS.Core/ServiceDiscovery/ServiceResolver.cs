namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

/// <summary>
/// Resolves full details of a service instance (hostname, port, addresses, properties)
/// </summary>
public class ServiceResolver
{
    private readonly PacketSender _sender;
    private readonly RecordCache _cache;
    private readonly ILogger? _logger;

    public ServiceResolver(PacketSender sender, RecordCache cache, ILogger? logger = null)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger;
    }

    /// <summary>
    /// Resolve a service instance to get full details
    /// </summary>
    public async Task<ServiceInstance?> ResolveAsync(string instanceName, string serviceType,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var fullName = $"{instanceName}.{serviceType}";

        _logger?.LogDebug("Resolving service: {FullName}", fullName);

        var instance = new ServiceInstance(fullName, instanceName, serviceType);

        // Try to get information from cache first
        bool needsQuery = !TryResolveFromCache(instance);

        if (needsQuery)
        {
            // Send queries for SRV, TXT, and wait for responses
            await _sender.SendQueryAsync(fullName, false, cancellationToken);

            // Wait a bit for responses
            await Task.Delay(timeout, cancellationToken);

            // Try again from cache
            TryResolveFromCache(instance);
        }

        if (instance.IsResolved)
        {
            _logger?.LogInformation("Resolved service: {Instance}", instance);
        }
        else
        {
            _logger?.LogWarning("Failed to fully resolve service: {FullName}", fullName);
        }

        return instance.IsResolved ? instance : null;
    }

    internal bool TryResolveFromCache(ServiceInstance instance)
    {
        bool hasHostname = false;
        bool hasAddresses = false;
        uint minTtl = instance.Ttl; // Start with existing TTL (from PTR record)

        _logger?.LogDebug("Attempting to resolve {FullName} from cache", instance.FullName);

        // Get SRV record for hostname and port
        var srvRecords = _cache.Get(instance.FullName, DnsType.SRV);
        _logger?.LogDebug("Found {Count} SRV records for {FullName}", srvRecords.Count, instance.FullName);

        if (srvRecords.Count > 0 && srvRecords[0] is SrvRecord srvRecord)
        {
            instance.HostName = srvRecord.Target;
            instance.Port = srvRecord.Port;
            instance.Priority = srvRecord.Priority;
            instance.Weight = srvRecord.Weight;
            hasHostname = true;
            minTtl = Math.Min(minTtl, srvRecord.Ttl);

            _logger?.LogDebug("Found SRV: {HostName}:{Port} (TTL: {Ttl})", instance.HostName, instance.Port, srvRecord.Ttl);
        }
        else
        {
            _logger?.LogDebug("No SRV record found for {FullName}", instance.FullName);
        }

        // Get TXT record for properties
        var txtRecords = _cache.Get(instance.FullName, DnsType.TXT);
        if (txtRecords.Count > 0 && txtRecords[0] is TxtRecord txtRecord)
        {
            instance.TxtRecords = txtRecord.Attributes;
            minTtl = Math.Min(minTtl, txtRecord.Ttl);
            _logger?.LogDebug("Found TXT with {Count} attributes (TTL: {Ttl})", txtRecord.Attributes.Count, txtRecord.Ttl);
        }

        // Get A/AAAA records for addresses
        if (hasHostname && !string.IsNullOrEmpty(instance.HostName))
        {
            var addresses = new List<IPAddress>();

            var aRecords = _cache.Get(instance.HostName, DnsType.A);
            _logger?.LogDebug("Found {Count} A records for {HostName}", aRecords.Count, instance.HostName);

            foreach (var record in aRecords.OfType<ARecord>())
            {
                addresses.Add(record.Address);
                minTtl = Math.Min(minTtl, record.Ttl);
                _logger?.LogDebug("  A record: {Address} (TTL: {Ttl})", record.Address, record.Ttl);
            }

            var aaaaRecords = _cache.Get(instance.HostName, DnsType.AAAA);
            _logger?.LogDebug("Found {Count} AAAA records for {HostName}", aaaaRecords.Count, instance.HostName);

            foreach (var record in aaaaRecords.OfType<AAAARecord>())
            {
                addresses.Add(record.Address);
                minTtl = Math.Min(minTtl, record.Ttl);
                _logger?.LogDebug("  AAAA record: {Address} (TTL: {Ttl})", record.Address, record.Ttl);
            }

            if (addresses.Count > 0)
            {
                instance.Addresses = addresses.ToArray();
                hasAddresses = true;
                _logger?.LogDebug("Total {Count} addresses found", addresses.Count);
            }
            else
            {
                _logger?.LogDebug("No addresses found for {HostName}", instance.HostName);
            }
        }

        instance.IsResolved = hasHostname && hasAddresses && instance.Port > 0;
        instance.LastSeen = DateTime.UtcNow;

        // Use the minimum TTL from all records - service expires when any record expires
        if (instance.IsResolved && minTtl < instance.Ttl)
        {
            _logger?.LogDebug("Updating service TTL from {OldTtl}s to {NewTtl}s (minimum of all records)", instance.Ttl, minTtl);
            instance.Ttl = minTtl;
        }

        return instance.IsResolved;
    }
}
