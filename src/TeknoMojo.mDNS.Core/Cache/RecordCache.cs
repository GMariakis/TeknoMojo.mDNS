namespace TeknoMojo.mDNS.Core.Cache;

/// <summary>
/// Thread-safe cache for DNS resource records with TTL-based expiration
/// </summary>
public class RecordCache : IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<CacheEntry>> _cache = new();
    private readonly ILogger? _logger;
    private readonly Timer? _cleanupTimer;
    private bool _disposed;

    public RecordCache(ILogger? logger = null)
    {
        _logger = logger;

        // Run cleanup every 30 seconds
        _cleanupTimer = new Timer(CleanupExpiredEntries, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Add or update a resource record in the cache
    /// </summary>
    public void AddOrUpdate(DnsResourceRecord record)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));

        var key = GetCacheKey(record.Name, record.Type);

        // Handle cache-flush bit (RFC 6762)
        if (record.CacheFlush)
        {
            // Remove existing records for this name/type
            _cache.TryRemove(key, out _);
            _logger?.LogDebug("Cache flushed for {Key}", key);
        }

        // Handle goodbye (TTL=0)
        if (record.Ttl == 0)
        {
            _cache.TryRemove(key, out _);
            _logger?.LogDebug("Removed record from cache (goodbye): {Key}", key);
            return;
        }

        var entry = new CacheEntry(record);

        _cache.AddOrUpdate(key,
            _ => new ConcurrentBag<CacheEntry> { entry },
            (_, existing) =>
            {
                // Check if an equivalent record already exists
                var existingList = existing.Where(e => e.IsValid).ToList();
                var duplicate = existingList.FirstOrDefault(e => RecordsAreEquivalent(e.Record, record));

                if (duplicate != null)
                {
                    // Update existing record by removing old and adding new
                    var newBag = new ConcurrentBag<CacheEntry>(existingList.Where(e => e != duplicate));
                    newBag.Add(entry);
                    _logger?.LogDebug("Updated existing record in cache: {Key}", key);
                    return newBag;
                }
                else
                {
                    // Add new unique record
                    existing.Add(entry);
                    _logger?.LogDebug("Added new record to cache: {Key}", key);
                    return existing;
                }
            });
    }

    /// <summary>
    /// Check if two DNS records are equivalent (same data content)
    /// </summary>
    private bool RecordsAreEquivalent(DnsResourceRecord record1, DnsResourceRecord record2)
    {
        if (record1.GetType() != record2.GetType())
            return false;

        // For PTR records, compare domain names
        if (record1 is Protocol.RecordTypes.PtrRecord ptr1 && record2 is Protocol.RecordTypes.PtrRecord ptr2)
        {
            return string.Equals(ptr1.DomainName, ptr2.DomainName, StringComparison.OrdinalIgnoreCase);
        }

        // For SRV records, compare priority, weight, port, and target
        if (record1 is Protocol.RecordTypes.SrvRecord srv1 && record2 is Protocol.RecordTypes.SrvRecord srv2)
        {
            return srv1.Priority == srv2.Priority &&
                   srv1.Weight == srv2.Weight &&
                   srv1.Port == srv2.Port &&
                   string.Equals(srv1.Target, srv2.Target, StringComparison.OrdinalIgnoreCase);
        }

        // For A records, compare addresses
        if (record1 is Protocol.RecordTypes.ARecord a1 && record2 is Protocol.RecordTypes.ARecord a2)
        {
            return a1.Address.Equals(a2.Address);
        }

        // For AAAA records, compare addresses
        if (record1 is Protocol.RecordTypes.AAAARecord aaaa1 && record2 is Protocol.RecordTypes.AAAARecord aaaa2)
        {
            return aaaa1.Address.Equals(aaaa2.Address);
        }

        // For TXT records, compare text data
        if (record1 is Protocol.RecordTypes.TxtRecord txt1 && record2 is Protocol.RecordTypes.TxtRecord txt2)
        {
            if (txt1.Attributes.Count != txt2.Attributes.Count)
                return false;

            foreach (var kvp in txt1.Attributes)
            {
                if (!txt2.Attributes.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                    return false;
            }
            return true;
        }

        // Default: assume different
        return false;
    }

    /// <summary>
    /// Get all valid records for a given name and type
    /// </summary>
    public List<DnsResourceRecord> Get(string name, DnsType type)
    {
        var key = GetCacheKey(name, type);

        if (!_cache.TryGetValue(key, out var entries))
        {
            return new List<DnsResourceRecord>();
        }

        return entries
            .Where(e => e.IsValid)
            .Select(e => e.Record)
            .ToList();
    }

    /// <summary>
    /// Get all valid records for a given name (all types)
    /// </summary>
    public List<DnsResourceRecord> GetAll(string name)
    {
        return _cache
            .Where(kvp => kvp.Key.StartsWith($"{name}|"))
            .SelectMany(kvp => kvp.Value)
            .Where(e => e.IsValid)
            .Select(e => e.Record)
            .ToList();
    }

    /// <summary>
    /// Clear all records for a specific name and type
    /// </summary>
    public void Flush(string name, DnsType type)
    {
        var key = GetCacheKey(name, type);
        _cache.TryRemove(key, out _);
        _logger?.LogDebug("Flushed cache for {Key}", key);
    }

    /// <summary>
    /// Clear the entire cache
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _logger?.LogInformation("Cache cleared");
    }

    /// <summary>
    /// Get the number of cached entries
    /// </summary>
    public int Count => _cache.Sum(kvp => kvp.Value.Count(e => e.IsValid));

    private string GetCacheKey(string name, DnsType type)
    {
        return $"{name.ToLowerInvariant()}|{type}";
    }

    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            int removedCount = 0;

            foreach (var kvp in _cache.ToArray())
            {
                var validEntries = kvp.Value.Where(e => e.IsValid).ToList();

                if (validEntries.Count == 0)
                {
                    // Remove the entire key if no valid entries
                    if (_cache.TryRemove(kvp.Key, out var removed))
                    {
                        removedCount += removed.Count;
                    }
                }
                else if (validEntries.Count < kvp.Value.Count)
                {
                    // Replace with only valid entries
                    var newBag = new ConcurrentBag<CacheEntry>(validEntries);
                    _cache.TryUpdate(kvp.Key, newBag, kvp.Value);
                    removedCount += kvp.Value.Count - validEntries.Count;
                }
            }

            if (removedCount > 0)
            {
                _logger?.LogDebug("Cleaned up {Count} expired cache entries", removedCount);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during cache cleanup");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
